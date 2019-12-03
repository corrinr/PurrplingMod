using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NpcAdventure.Loader;
using NpcAdventure.Model;
using NpcAdventure.Objects;
using NpcAdventure.StateMachine.State;
using NpcAdventure.StateMachine.StateFeatures;
using NpcAdventure.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using static NpcAdventure.NetCode.NetEvents;

namespace NpcAdventure.StateMachine
{

    internal class CompanionStateMachine
    {
        /// <summary>
        /// Allowed states in machine
        /// </summary>
        public enum StateFlag
        {
            RESET,
            AVAILABLE,
            RECRUITED,
            UNAVAILABLE,
        }
        public CompanionManager CompanionManager { get; private set; }
        public NPC Companion { get; private set; }
        public CompanionMetaData Metadata { get; }
        public IContentLoader ContentLoader { get; private set; }
        private IMonitor Monitor { get; }
        public Chest Bag { get; private set; }
        public IReflectionHelper Reflection { get; }
        public Dictionary<StateFlag, ICompanionState> States { get; private set; }
        public ICompanionState currentState { get; private set; }

        public CompanionStateMachine(CompanionManager manager, NPC companion, CompanionMetaData metadata, IContentLoader loader, IReflectionHelper reflection, IMonitor monitor = null)
        {
            this.CompanionManager = manager;
            this.Companion = companion;
            this.Metadata = metadata;
            this.ContentLoader = loader;
            this.Monitor = monitor;
            this.Bag = new Chest(true);
            this.Reflection = reflection;
        }

        /// <summary>
        /// Our companion name (Refers NPC name)
        /// </summary>
        public string Name
        {
            get
            {
                return this.Companion.Name;
            }
        }

        public StateFlag CurrentStateFlag { get; private set; }
        public Dictionary<int, SchedulePathDescription> BackedupSchedule { get; internal set; }
        public bool RecruitedToday { get; private set; }

        /// <summary>
        /// Change companion state machine state
        /// </summary>
        /// <param name="stateFlag">Flag of allowed state</param>
        private void ChangeState(StateFlag stateFlag, Farmer byWhom)
        {
            if (this.States == null)
                throw new InvalidStateException("State machine is not ready! Call setup first.");

            if (!this.States.TryGetValue(stateFlag, out ICompanionState newState))
                throw new InvalidStateException($"Invalid state {stateFlag.ToString()}. Is state machine correctly set up?");

            if (this.currentState == newState)
                return;

            if (this.currentState != null)
            {
                this.currentState.Exit();
            }

            newState.Entry(byWhom);
            this.currentState = newState;
            this.Monitor.Log($"{this.Name} changed state: {this.CurrentStateFlag.ToString()} -> {stateFlag.ToString()}");
            this.CurrentStateFlag = stateFlag;
        }

        /// <summary>
        /// Setup state handlers
        /// </summary>
        /// <param name="stateHandlers"></param>
        public void Setup(Dictionary<StateFlag, ICompanionState> stateHandlers)
        {
            if (this.States != null)
                throw new InvalidOperationException("State machine is already set up!");

            this.States = stateHandlers;
            this.ResetStateMachine();
        }

        /// <summary>
        /// Companion speaked a dialogue
        /// </summary>
        /// <param name="speakedDialogue"></param>
        public void DialogueSpeaked(Dialogue speakedDialogue)
        {
            // Convert state to dialogue detector (if state implements it)
            IDialogueDetector detector = this.currentState as IDialogueDetector;

            // TODO check if we can remove this cmopletely?
            /*if (detector != null)
            {
                detector.OnDialogueSpeaked(speakedDialogue.); // Handle this dialogue
            }*/
        }

        /// <summary>
        /// Setup companion for new day
        /// </summary>
        public void NewDaySetup()
        {
            if (!Game1.IsMasterGame)
                return;

            if (this.CurrentStateFlag != StateFlag.RESET)
                throw new InvalidStateException($"State machine {this.Name} must be in reset state!");

            // Today is festival day? Player can't recruit this companion
            if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.currentSeason))
            {
                this.Monitor.Log($"{this.Name} is unavailable to recruit due to festival today.");
                this.MakeUnavailable();
                return;
            }

            // Setup dialogues for companion for this day
            DialogueHelper.SetupCompanionDialogues(this.Companion, this.ContentLoader.LoadStrings($"Dialogue/{this.Name}"));

            // Spoused or married with her/him? Enhance dialogues with extra spouse dialogues for this day
            if (Helper.IsSpouseMarriedToFarmer(this.Companion, this.CompanionManager.Farmer) && this.ContentLoader.CanLoad($"Dialogue/{this.Name}Spouse"))
                DialogueHelper.SetupCompanionDialogues(this.Companion, this.ContentLoader.LoadStrings($"Dialogue/{this.Name}Spouse"));

            this.RecruitedToday = false;
            this.MakeAvailable();
        }

        /// <summary>
        /// Dump items from companion's bag to farmer (player) house
        /// </summary>
        public void DumpBagInFarmHouse()
        {
            FarmHouse farm = (FarmHouse)Game1.getLocationFromName("FarmHouse");
            Vector2 place = Utility.PointToVector2(farm.getRandomOpenPointInHouse(Game1.random));
            Package dumpedBag = new Package(this.Bag.items.ToList(), place)
            {
                GivenFrom = this.Name,
                Message = this.ContentLoader.LoadString("Strings/Strings:bagItemsSentLetter", this.CompanionManager.Farmer.Name, this.Companion.displayName)
            };

            farm.objects.Add(place, dumpedBag);
            this.Bag = new Chest(true);

            this.Monitor.Log($"{this.Companion} delivered bag contents into farm house at position {place}");
        }

        /// <summary>
        /// Does companion have this skill?
        /// </summary>
        /// <param name="skill">Which skill</param>
        /// <returns>True if companion has this skill, otherwise False</returns>
        public bool HasSkill(string skill)
        {
            return this.Metadata.PersonalSkills.Contains(skill);
        }

        /// <summary>
        /// Does companion have all of these skills?
        /// </summary>
        /// <param name="skills">Which skills</param>
        /// <returns>True if companion has all of them, otherwise False</returns>
        public bool HasSkills(params string[] skills)
        {
            foreach (string skill in skills)
            {
                if (!this.HasSkill(skill))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Does companion have any of these skills?
        /// </summary>
        /// <param name="skills">Which skills</param>
        /// <returns>True if companion have any of these skills, otherwise False</returns>
        public bool HasSkillsAny(params string[] skills)
        {
            foreach (string skill in skills)
            {
                if (this.HasSkill(skill))
                    return true;
            }

            return false;
        } 

        /// <summary>
        /// Make companion AVAILABLE to recruit
        /// </summary>
        public void MakeAvailable(Farmer byWhom = null)
        {
            this.CompanionManager.netEvents.FireEvent(new CompanionChangedState(this.Companion, StateFlag.AVAILABLE, byWhom), null, true);
        }

        public void MakeLocalAvailable(Farmer byWhom = null)
        {
            this.ChangeState(StateFlag.AVAILABLE, byWhom);
        }

        /// <summary>
        /// Make companion UNAVAILABLE to recruit
        /// </summary>
        public void MakeUnavailable(Farmer byWhom = null)
        {
            this.CompanionManager.netEvents.FireEvent(new CompanionChangedState(this.Companion, StateFlag.UNAVAILABLE, byWhom), null, true);
        }

        public void MakeLocalUnavailable(Farmer byWhom = null)
        {
            this.ChangeState(StateFlag.UNAVAILABLE, byWhom);
        }

        /// <summary>
        /// Reset companion's state machine
        /// </summary>
        public void ResetStateMachine(Farmer byWhom = null)
        {
            if (Game1.IsMasterGame)
              this.CompanionManager.netEvents.FireEvent(new CompanionChangedState(this.Companion, StateFlag.RESET, byWhom), null, true);
        }

        public void ResetLocalStateMachine(Farmer byWhom = null)
        {
            this.ChangeState(StateFlag.RESET, byWhom);
        }

        /// <summary>
        /// Dismiss recruited companion
        /// </summary>
        /// <param name="keepUnavailableOthers">Keep other companions unavailable?</param>
        internal void Dismiss(bool keepUnavailableOthers = false, Farmer byWhom = null)
        {
            this.ResetStateMachine(byWhom);

            if (this.currentState is ICompanionIntegrator integrator)
                integrator.ReintegrateCompanionNPC();

            this.BackedupSchedule = null;
            //this.ChangeState(StateFlag.UNAVAILABLE, byWhom);
            this.MakeUnavailable(byWhom); // TODO make sure that somebody else when asked gets the message that the NPC has already been claimed today
            this.CompanionManager.CompanionDissmised(keepUnavailableOthers);
        }

        /// <summary>
        /// Recruit this companion
        /// </summary>
        public void Recruit(Farmer byWhom)
        {
            this.BackedupSchedule = this.Companion.Schedule;
            this.RecruitedToday = true;

            // If standing on unpassable tile (like chair, couch or bench), set position to heading passable tile location
            if (!this.Companion.currentLocation.isTilePassable(this.Companion.GetBoundingBox(), Game1.viewport))
            {
                this.Companion.setTileLocation(this.Companion.GetGrabTile());
            }

            this.CompanionManager.netEvents.FireEvent(new CompanionChangedState(this.Companion, StateFlag.RECRUITED, byWhom), null, true);
        }

        public void RecruitLocally(Farmer byWhom)
        {
            this.ChangeState(StateFlag.RECRUITED, byWhom);
            this.CompanionManager.CompanionRecuited(this.Companion.Name, byWhom);
        }

        public void Dispose()
        {
            if (this.currentState != null)
                this.currentState.Exit();

            this.States.Clear();
            this.States = null;
            this.currentState = null;
            this.Companion = null;
            this.CompanionManager = null;
            this.ContentLoader = null;
        }

        /// <summary>
        /// Resolve dialogue request
        /// </summary>
        public void ResolveDialogueRequest(string answer = null)
        {
            // Can this companion to resolve player's dialogue request?
            if (!this.CanDialogueRequestResolve())
                return;

            // Handle dialogue request resolution in current machine state
            
            (this.currentState as IRequestedDialogueCreator).CreateRequestedDialogue();
        }

        /// <summary>
        /// Can request a dialogue for this companion in current state?
        /// </summary>
        /// <returns>True if dialogue request can be resolved</returns>
        public bool CanDialogueRequestResolve()
        {
            return this.currentState is IRequestedDialogueCreator dcreator && dcreator.CanCreateDialogue;
        }
    }

    class InvalidStateException : Exception
    {
        public InvalidStateException(string message) : base(message)
        {
        }
    }
}
