using System;
using System.Collections.Generic;
using NpcAdventure.StateMachine;
using StardewValley;
using StardewModdingAPI;
using NpcAdventure.Driver;
using NpcAdventure.Loader;
using StardewModdingAPI.Events;
using NpcAdventure.Utils;
using NpcAdventure.StateMachine.State;
using static NpcAdventure.StateMachine.CompanionStateMachine;
using NpcAdventure.Model;
using NpcAdventure.Events;
using NpcAdventure.NetCode;
using NpcAdventure.HUD;
using NpcAdventure.Story;
using NpcAdventure.Story.Messaging;
using static NpcAdventure.NetCode.NetEvents;

namespace NpcAdventure
{
    internal class CompanionManager
    {
        private readonly DialogueDriver dialogueDriver;
        private readonly HintDriver hintDriver;
        private readonly IMonitor monitor;

        public Dictionary<string, CompanionStateMachine> PossibleCompanions { get; }
        public IGameMaster GameMaster { get; }
        public CompanionDisplay Hud { get; }
        public Config Config { get; }
        public NetEvents netEvents;

        private IContentLoader loader;

        public Farmer Farmer
        {
            get
            {
                if (Context.IsWorldReady)
                    return Game1.player;
                return null;
            }
        }

        public CompanionManager(IGameMaster gameMaster, DialogueDriver dialogueDriver, HintDriver hintDriver, CompanionDisplay hud, Config config, IMonitor monitor, NetEvents netEvents)
        {
            this.GameMaster = gameMaster ?? throw new ArgumentNullException(nameof(gameMaster));
            this.dialogueDriver = dialogueDriver ?? throw new ArgumentNullException(nameof(dialogueDriver));
            this.hintDriver = hintDriver ?? throw new ArgumentNullException(nameof(hintDriver));
            this.Hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            this.PossibleCompanions = new Dictionary<string, CompanionStateMachine>();
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
            this.netEvents = netEvents;

            this.dialogueDriver.DialogueChanged += this.DialogueDriver_DialogueChanged;
            this.hintDriver.CheckHint += this.HintDriver_CheckHint;
        }

        /// <summary>
        /// Dialogue event handler. 
        /// Works with previous dialogue when dialogue has been changed
        /// </summary>
        /// <param name="sender">Who sent it?</param>
        /// <param name="e">Changed dialogue event arguments</param>
        private void DialogueDriver_DialogueChanged(object sender, DialogueChangedArgs e)
        {
            NPC n = e.PreviousDialogue?.speaker; // Who said previous dialogue?

            // No previous dialogue? Forget it.
            if (e.PreviousDialogue == null || n == null)
                return;

            // Check if spoken it any our companion
            if (this.PossibleCompanions.TryGetValue(n.Name, out CompanionStateMachine csm))
            {
                csm.DialogueSpeaked(e.PreviousDialogue); // Companion can react on this dialogue
            }
        }

        internal void UpdateNPC(NpcListChangedEventArgs e)
        {
            foreach(NPC n in e.Added)
            {
                CompanionStateMachine csm = this.PossibleCompanions[n.Name];
                if (csm != null)
                {
                    csm.ReseatCompanion(n);
                }
            }
        }

        internal void SyncLocalBags()
        {
            foreach(var csmKv in this.PossibleCompanions)
            {
                if (csmKv.Value.currentState.GetByWhom() == Game1.player && csmKv.Value.CurrentStateFlag == StateFlag.RECRUITED)
                    this.netEvents.FireEvent(new SendBagEvent(csmKv.Value.Companion, csmKv.Value.Bag));
            }
        }

        /// <summary>
        /// Handle check hint event from event driver
        /// </summary>
        /// <param name="sender">Who sent this event?</param>
        /// <param name="e">Hint checker event arguments</param>
        private void HintDriver_CheckHint(object sender, CheckHintArgs e)
        {
            if (e.Npc == null)
                return;

            // Show dialogue icon hint (NPC can speak bubble) if:
            // - cursor is on our companion 
            // - and they can resolve our dialogue requests 
            // - and has'nt any dialogues in queue 
            // - and we can ask this companion for following (recruit)
            if (this.PossibleCompanions.TryGetValue(e.Npc.Name, out CompanionStateMachine csm)
                && this.CanRecruit()
                && csm.Name == e.Npc?.Name
                && csm.CanPerformAction()
                && e.Npc.CurrentDialogue.Count == 0
                && Helper.CanRequestDialog(this.Farmer, e.Npc, csm.CurrentStateFlag == StateFlag.RECRUITED))
            {
                this.hintDriver.ShowHint(HintDriver.Hint.DIALOGUE);
            }
        }

        public bool CheckAction(Farmer who, NPC withWhom, GameLocation location)
        {
            if (this.PossibleCompanions.TryGetValue(withWhom.Name, out CompanionStateMachine csm) && csm.Name == withWhom.Name)
            {
                return csm.CheckAction(who, location);
            }

            return false;
        }

        internal bool CanRecruit()
        {
            if (!Context.IsWorldReady || this.Farmer == null)
            {
                return false;
            }

            if (this.GameMaster.Mode == GameMasterMode.OFFLINE)
                return true; // In non-adventure mode we can recruit a companion

            return this.GameMaster.Data.GetPlayerState(this.Farmer).isEligible;
        }

        /// <summary>
        /// When any companion was recruited
        /// </summary>
        /// <param name="companionName">NPC name of companion</param>
        internal void CompanionRecuited(string companionName, Farmer byWhom)
        {
            this.monitor.Log($"You are recruited {companionName} companion.");
        }

        /// <summary>
        /// Reset all state machines of companions
        /// </summary>
        public void ResetStateMachines()
        {
            foreach (var companionKv in this.PossibleCompanions)
                companionKv.Value.ResetStateMachine(null);
        }

        /// <summary>
        /// Setup all companions for new day
        /// </summary>
        public void NewDaySetup()
        {
            try
            {
                // For each companion state machine call new day setup method
                foreach (var companionKv in this.PossibleCompanions)
                    companionKv.Value.NewDaySetup();

                this.monitor.Log("Companions are successfully setup for new day!", LogLevel.Info);
            }
            catch (InvalidStateException e)
            {
                this.monitor.Log($"Error while trying to setup new day: {e.Message}");
            }
        }

        /// <summary>
        /// Dump player's items from companion's bag
        /// </summary>
        public void DumpCompanionNonEmptyBags()
        {
            foreach (var csm in this.PossibleCompanions)
                if (!csm.Value.Bag.isEmpty())
                    csm.Value.DumpBagInFarmHouse();
        }

        /// <summary>
        /// When any companion dissmised (relieved from duty, unfollow player)
        /// </summary>
        /// <param name="keepUnavailable">Set this companion unavailable after dismiss?</param>
        internal void CompanionDissmised(Farmer byWhom, bool keepUnavailable = false)
        {
            /*foreach (var csmKv in this.PossibleCompanions)
            {
                if (keepUnavailable)
                    csmKv.Value.MakeUnavailable(byWhom);
                else if (!csmKv.Value.RecruitedToday)
                    csmKv.Value.MakeAvailable(byWhom);
            }*/
        }

        /// <summary>
        /// Companion initializator. Call it after saved game is loaded
        /// </summary>
        /// <param name="loader"></param>
        /// <param name="gameEvents"></param>
        /// <param name="reflection"></param>
        public void InitializeCompanions(IContentLoader loader, IModEvents gameEvents, ISpecialModEvents specialEvents, IReflectionHelper reflection)
        {
            Dictionary<string, string> dispositions = loader.Load<Dictionary<string, string>>("Data/CompanionDispositions");

            this.PossibleCompanions.Clear();
            foreach (string npcName in dispositions.Keys)
            {
                NPC companion = Game1.getCharacterFromName(npcName, true);

                if (companion == null)
                    throw new Exception($"Can't find NPC with name '{npcName}'");

                CompanionStateMachine csm = new CompanionStateMachine(this, companion, new CompanionMetaData(dispositions[npcName]), loader, reflection, this.monitor);
                Dictionary<StateFlag, ICompanionState> stateHandlers = new Dictionary<StateFlag, ICompanionState>()
                {
                    [StateFlag.RESET] = new ResetState(csm, gameEvents, this.monitor),
                    [StateFlag.AVAILABLE] = new AvailableState(csm, gameEvents, this.monitor),
                    [StateFlag.RECRUITED] = new RecruitedState(csm, gameEvents, specialEvents, this.monitor, this.netEvents),
                    [StateFlag.UNAVAILABLE] = new UnavailableState(csm, gameEvents, this.monitor),
                };

                this.PossibleCompanions.Add(npcName, csm);
                csm.Setup(stateHandlers);
            }

            this.monitor.Log($"Initalized {this.PossibleCompanions.Count} companions.", LogLevel.Info);
            this.loader = loader;
        }

        /// <summary>
        /// Companion uninitalizer. Call it after game exited (return to title)
        /// </summary>
        public void UninitializeCompanions()
        {
            foreach (var companionKv in this.PossibleCompanions)
            {
                companionKv.Value.Dispose();
                this.monitor.Log($"{companionKv.Key} disposed!");
            }

            this.PossibleCompanions.Clear();
            this.monitor.Log("Companions uninitialized", LogLevel.Info);
        }

        internal void PlayerDisconnected(Farmer farmer)
        {
            foreach (var companionKv in this.PossibleCompanions)
            {
                if (companionKv.Value.CurrentStateFlag == StateFlag.RECRUITED && companionKv.Value.currentState.GetByWhom() == farmer)
                {
                    this.monitor.Log("Resetting companion " + companionKv.Key + " to unavailable state as player has disconnected.");
                    companionKv.Value.MakeUnavailable(farmer);
                }
            }
        }
    }
}
