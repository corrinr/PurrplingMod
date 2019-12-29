using NpcAdventure.StateMachine.StateFeatures;
using NpcAdventure.Utils;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using StardewValley.Locations;
using Microsoft.Xna.Framework;
using System.Reflection;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using NpcAdventure.Buffs;
using StardewModdingAPI;
using NpcAdventure.AI;
using Microsoft.Xna.Framework.Graphics;
using NpcAdventure.Events;
using static NpcAdventure.NetCode.NetEvents;
using NpcAdventure.NetCode;

namespace NpcAdventure.StateMachine.State
{
    internal class RecruitedState : CompanionState, IRequestedDialogueCreator, IDialogueDetector
    {
        public AI_StateMachine ai;
        private Dialogue dismissalDialogue;
        private Dialogue currentLocationDialogue;

        public bool CanCreateDialogue { get; private set; }
        private BuffManager BuffManager { get; set; }
        private NetEvents netEvents;
        public ISpecialModEvents SpecialEvents { get; }

        public RecruitedState(CompanionStateMachine stateMachine, IModEvents events, ISpecialModEvents specialEvents, IMonitor monitor, NetEvents netEvents) : base(stateMachine, events, monitor)
        {
            this.BuffManager = new BuffManager(stateMachine.Companion, stateMachine.CompanionManager.Farmer, stateMachine.ContentLoader);
            this.SpecialEvents = specialEvents;
            this.netEvents = netEvents;
        }

        public override void Entry(Farmer byWhom)
        {
            this.setByWhom = byWhom;

            this.ai = new AI_StateMachine(this.StateMachine, this.setByWhom, this.StateMachine.CompanionManager.Hud, this.Events, this.monitor, this.netEvents);

            if (this.StateMachine.Companion.doingEndOfRouteAnimation.Value)
                this.FinishScheduleAnimation();

            this.StateMachine.Companion.faceTowardFarmerTimer = 0;
            this.StateMachine.Companion.movementPause = 0;
            this.StateMachine.Companion.followSchedule = false;
            this.StateMachine.Companion.Schedule = null;
            this.StateMachine.Companion.controller = null;
            this.StateMachine.Companion.temporaryController = null;
            this.StateMachine.Companion.eventActor = true;
            this.StateMachine.Companion.farmerPassesThrough = true;

            if (this.StateMachine.Companion.isMarried() && Patches.SpouseReturnHomePatch.recruitedSpouses.IndexOf(this.StateMachine.Companion.Name) < 0)
            {
                // Avoid returning recruited wife/husband to FarmHouse when is on Farm and it's after 1pm
                Patches.SpouseReturnHomePatch.recruitedSpouses.Add(this.StateMachine.Companion.Name);
            }

            this.Events.Player.Warped += this.Player_Warped;
            this.SpecialEvents.RenderedLocation += this.SpecialEvents_RenderedLocation;

            this.Events.GameLoop.UpdateTicked += this.GameLoop_UpdateTicked;

            if (Game1.IsMasterGame)
            {
                
                this.Events.GameLoop.TimeChanged += this.GameLoop_TimeChanged;
            }

            if (this.BuffManager.HasAssignableBuffs())
                this.BuffManager.AssignBuffs();
            else
                this.monitor.Log($"Companion {this.StateMachine.Name} has no buffs defined!", LogLevel.Alert);

            if (DialogueHelper.GetVariousDialogueString(this.StateMachine.Companion, "companionRecruited", out string dialogueText))
                this.StateMachine.Companion.setNewDialogue(dialogueText);
            this.CanCreateDialogue = true;

            this.ai.Setup();

            if (byWhom == Game1.player)
            {
                foreach (string skill in this.StateMachine.Metadata.PersonalSkills)
                {
                    string text = this.StateMachine.ContentLoader.LoadString($"Strings/Strings:skill.{skill}", this.StateMachine.Companion.displayName)
                            + Environment.NewLine
                            + this.StateMachine.ContentLoader.LoadString($"Strings/Strings:skillDescription.{skill}");

                    this.StateMachine.CompanionManager.Hud.AddSkill(skill, text);
                }
                this.StateMachine.CompanionManager.Hud.AssignCompanion(this.StateMachine.Companion);
            }

            
        }

        private void SpecialEvents_RenderedLocation(object sender, ILocationRenderedEventArgs e)
        {
            if (this.ai != null)
            {
                this.ai.Draw(e.SpriteBatch);
            }
        }

        /// <summary>
        /// Animate last sequence of current schedule animation
        /// </summary>
        private void FinishScheduleAnimation()
        {
            // Prevent animation freeze glitch
            this.StateMachine.Companion.Sprite.standAndFaceDirection(this.StateMachine.Companion.FacingDirection);

            // And then play finish animation "end of route animation" when companion is recruited
            // Must be called via reflection, because they are private members of NPC class
            this.StateMachine.Reflection.GetMethod(this.StateMachine.Companion, "finishEndOfRouteAnimation").Invoke();
            this.StateMachine.Companion.doingEndOfRouteAnimation.Value = false;
            this.StateMachine.Reflection.GetField<Boolean>(this.StateMachine.Companion, "currentlyDoingEndOfRouteAnimation").SetValue(false);
        }

        public override void Exit()
        {
            this.BuffManager.ReleaseBuffs();
            if (this.ai != null)
            {
                this.ai.Dispose();
            }

            if (Patches.SpouseReturnHomePatch.recruitedSpouses.IndexOf(this.StateMachine.Companion.Name) >= 0)
            {
                // Allow dissmised wife/husband to return to FarmHouse when is on Farm
                Patches.SpouseReturnHomePatch.recruitedSpouses.Remove(this.StateMachine.Companion.Name);
            }

            this.StateMachine.Companion.eventActor = false;
            this.StateMachine.Companion.farmerPassesThrough = false;
            this.CanCreateDialogue = false;

            this.SpecialEvents.RenderedLocation -= this.SpecialEvents_RenderedLocation;
            this.Events.GameLoop.UpdateTicked -= this.GameLoop_UpdateTicked;
            this.Events.GameLoop.TimeChanged -= this.GameLoop_TimeChanged;
            this.Events.Player.Warped -= this.Player_Warped;

            this.ai = null;
            this.dismissalDialogue = null;
            if (this.GetByWhom() == Game1.player)
                this.StateMachine.CompanionManager.Hud.Reset();
        }

        private void GameLoop_TimeChanged(object sender, TimeChangedEventArgs e)
        {
            this.StateMachine.Companion.clearSchedule();

            if (e.NewTime >= 2200)
            {
                NPC companion = this.StateMachine.Companion;
                Dialogue dismissalDialogue = new Dialogue(DialogueHelper.GetDialogueString(companion, "companionDismissAuto"), companion);
                this.dismissalDialogue = dismissalDialogue;
                this.StateMachine.Companion.doEmote(24);
                this.StateMachine.Companion.updateEmote(Game1.currentGameTime);
                DialogueHelper.DrawDialogue(dismissalDialogue);
            }

            MineShaft mines = this.StateMachine.Companion.currentLocation as MineShaft;

            // Fix spawn ladder if area is infested and all monsters is killed but NPC following us
            if (mines != null && mines.mustKillAllMonstersToAdvance())
            {
                var monsters = from c in mines.characters where c.IsMonster select c;
                if (monsters.Count() == 0)
                {
                    Vector2 vector2 = this.StateMachine.Reflection.GetProperty<Vector2>(mines, "tileBeneathLadder").GetValue();
                    if (mines.getTileIndexAt(Utility.Vector2ToPoint(vector2), "Buildings") == -1)
                        mines.createLadderAt(vector2, "newArtifact");
                }
            }
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (e.IsMultipleOf(20))
                this.FixProblemsWithNPC();

            if (this.ai != null)
                this.ai.Update(e);
        }

        private void FixProblemsWithNPC()
        {
            this.StateMachine.Companion.movementPause = 0;
            this.StateMachine.Companion.followSchedule = false;
            this.StateMachine.Companion.Schedule = null;
            this.StateMachine.Companion.controller = null;
            this.StateMachine.Companion.temporaryController = null;
            this.StateMachine.Companion.eventActor = true;
        }

        public void PlayerHasWarped(GameLocation from, GameLocation to)
        {
            NPC companion = this.StateMachine.Companion;
            Dictionary<string, string> bubbles = this.StateMachine.ContentLoader.LoadStrings("Strings/SpeechBubbles");

            // Warp companion to farmer if it's needed
            if (companion.currentLocation != to)
            {
                NpcAdventureMod.GameMonitor.Log("Warping NPC " + this.StateMachine.Companion.Name + " to a new location " + to.Name);
                this.ai.ChangeLocation(to);
            }

            // Show above head bubble text for location
            if (Game1.random.NextDouble() > 66f && DialogueHelper.GetBubbleString(bubbles, companion, to, out string bubble))
                companion.showTextAboveHead(bubble, preTimer: 250);

            // Push new location dialogue
            this.TryPushLocationDialogue(from);
        }

        private void Player_Warped(object sender, WarpedEventArgs e)
        {
            this.StateMachine.CompanionManager.netEvents.FireEvent(new PlayerWarpedEvent(this.StateMachine.Companion, e.OldLocation, e.NewLocation));
        }

        private bool TryPushLocationDialogue(GameLocation location)
        {
            NPC companion = this.StateMachine.Companion;
            Dialogue newDialogue = DialogueHelper.GenerateDialogue(companion, location, "companion");
            Stack<Dialogue> temp = new Stack<Dialogue>(this.StateMachine.Companion.CurrentDialogue.Count);

            if ((newDialogue == null && this.currentLocationDialogue == null) || (newDialogue != null && newDialogue.Equals(this.currentLocationDialogue)))
                return false;

            // Remove old location dialogue
            while (this.StateMachine.Companion.CurrentDialogue.Count > 0)
            {
                Dialogue d = this.StateMachine.Companion.CurrentDialogue.Pop();

                if (!d.Equals(this.currentLocationDialogue))
                    temp.Push(d);
            }

            while (temp.Count > 0)
                this.StateMachine.Companion.CurrentDialogue.Push(temp.Pop());

            this.currentLocationDialogue = newDialogue;

            if (newDialogue != null)
            {
                this.StateMachine.Companion.CurrentDialogue.Push(newDialogue); // Push new location dialogue
                return true;
            }

            return false;
        }

        public void CreateRequestedDialogue()
        {
            if (this.ai != null && this.ai.PerformAction())
                return;

            string[] answers = { "bag", "dismiss", "nothing" };

            this.StateMachine.CompanionManager.netEvents.FireEvent(new QuestionEvent("recruitedWant", this.StateMachine.Companion, answers), this.setByWhom);
            
            /*
            string question = this.StateMachine.ContentLoader.LoadString("Strings/Strings:recruitedWant");
            Response[] responses =
            {
                new Response("bag", this.StateMachine.ContentLoader.LoadString("Strings/Strings:recruitedWant.bag")),
                new Response("dismiss", this.StateMachine.ContentLoader.LoadString("Strings/Strings:recruitedWant.dismiss")),
                new Response("nothing", this.StateMachine.ContentLoader.LoadString("Strings/Strings:recruitedWant.nothing")),
            };

            location.createQuestionDialogue(question, responses, (_, answer) => {
                if (answer != "nothing")
                {
                    this.StateMachine.Companion.Halt();
                    this.StateMachine.Companion.facePlayer(leader);
                    this.ReactOnAsk(this.StateMachine.Companion, leader, answer);
                }
            }, this.StateMachine.Companion);*/
        }

        public void OnDialogueSpeaked(string question, string response)
        {
            if(question == "recruitedWant")
            {
                switch(response)
                {
                    case "nothing":
                        break;
                    case "dismiss":
                        this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent("companionDismiss", this.StateMachine.Companion), this.setByWhom);
                        this.StateMachine.CompanionManager.netEvents.FireEvent(new CompanionDismissEvent(this.StateMachine.Companion), Game1.MasterPlayer);
                        Game1.fadeScreenToBlack();
                        break;
                    case "bag": // TODO move to server syncing somehow, no idea how this works!
                        Chest bag = this.StateMachine.Bag;
                        this.StateMachine.Companion.currentLocation.playSound("openBox");
                        Game1.activeClickableMenu = new ItemGrabMenu(bag.items, false, true, new InventoryMenu.highlightThisItem(InventoryMenu.highlightAllItems), new ItemGrabMenu.behaviorOnItemSelect(bag.grabItemFromInventory), this.StateMachine.Companion.displayName, new ItemGrabMenu.behaviorOnItemSelect(bag.grabItemFromChest), false, true, true, true, true, 1, null, -1, this.StateMachine.Companion);
                        break;
                }
            }
        }
    }
}
