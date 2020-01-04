using NpcAdventure.StateMachine.StateFeatures;
using NpcAdventure.Utils;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using StardewValley.Locations;
using Microsoft.Xna.Framework;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using NpcAdventure.Buffs;
using StardewModdingAPI;
using NpcAdventure.AI;
using NpcAdventure.Events;
using static NpcAdventure.NetCode.NetEvents;
using NpcAdventure.NetCode;
using NpcAdventure.Internal;

namespace NpcAdventure.StateMachine.State
{
    internal class RecruitedState : CompanionState, IActionPerformer, IDialogueDetector
    {
        public AI_StateMachine ai;
        private Dialogue dismissalDialogue;
        private Dialogue currentLocationDialogue;
        private Dialogue recruitedDialogue;
        private int dialoguePushTime;
        private int timeOfRecruit;

        public bool CanPerformAction { get; private set; }
        private BuffManager BuffManager { get; set; }
        private NetEvents netEvents;
        public ISpecialModEvents SpecialEvents { get; }

        public RecruitedState(CompanionStateMachine stateMachine, IModEvents events, ISpecialModEvents specialEvents, IMonitor monitor, NetEvents netEvents) : base(stateMachine, events, monitor)
        {
            this.BuffManager = new BuffManager(stateMachine.Companion, stateMachine.CompanionManager.Farmer, stateMachine.ContentLoader, this.monitor);
            this.SpecialEvents = specialEvents;
            this.netEvents = netEvents;
        }

        public override void Entry(Farmer byWhom)
        {
            this.setByWhom = byWhom;

            this.ai = new AI_StateMachine(this.StateMachine, this.setByWhom, this.StateMachine.CompanionManager.Hud, this.Events, this.monitor, this.netEvents);
            this.timeOfRecruit = Game1.timeOfDay;

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

            this.Events.Player.Warped += this.Player_Warped;
            this.Events.Input.ButtonPressed += this.Input_ButtonPressed;
            this.SpecialEvents.RenderedLocation += this.SpecialEvents_RenderedLocation;

            this.Events.GameLoop.UpdateTicked += this.GameLoop_UpdateTicked;

            if (Game1.IsMasterGame)
            {
                
                this.Events.GameLoop.TimeChanged += this.GameLoop_TimeChanged;
            }

            this.BuffManager.AssignBuffs();
            this.recruitedDialogue = DialogueHelper.GenerateDialogue(this.StateMachine.Companion, "companionRecruited");
            this.CanPerformAction = true;

            if (this.recruitedDialogue != null)
                this.StateMachine.Companion.CurrentDialogue.Push(this.recruitedDialogue);

            this.ai.Setup();

            if (byWhom == Game1.player)
            {
                foreach (string skill in this.StateMachine.Metadata.PersonalSkills)
                {
                    string text = this.StateMachine.ContentLoader.LoadString($"Strings/Strings:skill.{skill}", this.StateMachine.Companion.displayName)
                            + Environment.NewLine
                            + this.StateMachine.ContentLoader.LoadString($"Strings/Strings:skillDescription.{skill}").Replace("#", Environment.NewLine);
                    this.StateMachine.CompanionManager.Hud.AddSkill(skill, text);
                }
            }

            this.StateMachine.CompanionManager.Hud.AssignCompanion(this.StateMachine.Companion);
            this.BuffManager.AssignBuffs();
            this.ai.Setup();

            if (this.BuffManager.HasProsthetics())
            {
                var key = this.StateMachine.CompanionManager.Config.ChangeBuffButton;
                var desc = this.StateMachine.ContentLoader.LoadString("Strings/Strings:prosteticsChangeButton", key, this.StateMachine.Companion.displayName);
                this.StateMachine.CompanionManager.Hud.AddKey(key, desc);
            }

        }

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (e.IsDown(this.StateMachine.CompanionManager.Config.ChangeBuffButton))
            {
                this.BuffManager.ChangeBuff();
            }
        }

        private void SpecialEvents_RenderedLocation(object sender, ILocationRenderedEventArgs e)
        {
            if (Game1.getCharacterFromName(this.StateMachine.Companion.Name).currentLocation == Game1.currentLocation)
                if (this.ai != null)
                    this.ai.Draw(e.SpriteBatch);
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

            this.StateMachine.Companion.eventActor = false;
            this.StateMachine.Companion.farmerPassesThrough = false;
            this.CanPerformAction = false;

            this.SpecialEvents.RenderedLocation -= this.SpecialEvents_RenderedLocation;
            this.Events.Input.ButtonPressed -= this.Input_ButtonPressed;
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
                Dialogue dismissalDialogue = new Dialogue(DialogueHelper.GetSpecificDialogueText(companion, this.StateMachine.CompanionManager.Farmer, "companionDismissAuto"), companion);
                this.dismissalDialogue = dismissalDialogue;
                this.StateMachine.Companion.doEmote(24);
                this.StateMachine.Companion.updateEmote(Game1.currentGameTime);
                DialogueHelper.DrawDialogue(dismissalDialogue);
            }

            // Fix spawn ladder if area is infested and all monsters is killed but NPC following us
            if (this.StateMachine.Companion.currentLocation is MineShaft mines && mines.mustKillAllMonstersToAdvance())
            {
                var monsters = from c in mines.characters where c.IsMonster select c;
                if (monsters.Count() == 0)
                {
                    Vector2 vector2 = this.StateMachine.Reflection.GetProperty<Vector2>(mines, "tileBeneathLadder").GetValue();
                    if (mines.getTileIndexAt(Utility.Vector2ToPoint(vector2), "Buildings") == -1)
                        mines.createLadderAt(vector2, "newArtifact");
                }
            }

            // Try to push new or change location dialogue randomly until or no location dialogue was pushed
            int until = this.dialoguePushTime + (Game1.random.Next(1, 3) * 10);
            if ((e.NewTime > until || this.currentLocationDialogue == null))
                this.TryPushLocationDialogue(this.StateMachine.Companion.currentLocation);

            // Remove recruited dialogue if this dialogue not spoken until a hour from while companion was recruited
            if (this.recruitedDialogue != null && e.NewTime > this.timeOfRecruit + 100)
            {
                // TODO: Use here Remove old dialogue method when rebased onto branch or merged branch which has this util
                Stack<Dialogue> temp = new Stack<Dialogue>(this.StateMachine.Companion.CurrentDialogue.Count);

                while (this.StateMachine.Companion.CurrentDialogue.Count > 0)
                {
                    Dialogue d = this.StateMachine.Companion.CurrentDialogue.Pop();

                    if (!d.Equals(this.recruitedDialogue))
                        temp.Push(d);
                    else
                        this.monitor.Log($"Recruited dialogue was removed from {this.StateMachine.Name}'s stack due to NPC was recruited a hour ago and dialogue still not spoken.");
                }

                while (temp.Count > 0)
                    this.StateMachine.Companion.CurrentDialogue.Push(temp.Pop());
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
            this.StateMachine.CompanionManager.netEvents.FireEvent(new PlayerWarpedEvent(this.StateMachine.Companion, e.OldLocation, e.NewLocation), null, true);
        }

        private void TryPushLocationDialogue(GameLocation location, bool warped = false)
        {
            Stack<Dialogue> temp = new Stack<Dialogue>(this.StateMachine.Companion.CurrentDialogue.Count);
            Dialogue newDialogue = this.StateMachine.GenerateLocationDialogue(location, warped ? "Enter" : "");

            if (warped && newDialogue == null)
            {
                // Try generate regular location dialogue if no enter location dialogue not defined or already spoken
                newDialogue = this.StateMachine.GenerateLocationDialogue(location);
            }

            bool isSameDialogue = this.currentLocationDialogue is CompanionDialogue curr
                                  && newDialogue is CompanionDialogue newd
                                  && curr.Kind == newd.Kind;

            if (isSameDialogue || (newDialogue == null && this.currentLocationDialogue == null))
                return;

            // Remove old location dialogue
            while (this.StateMachine.Companion.CurrentDialogue.Count > 0)
            {
                Dialogue d = this.StateMachine.Companion.CurrentDialogue.Pop();

                if (!d.Equals(this.currentLocationDialogue))
                    temp.Push(d);
                else
                    this.monitor.Log($"Old location dialogue was removed from {this.StateMachine.Name}'s stack");
            }

            while (temp.Count > 0)
                this.StateMachine.Companion.CurrentDialogue.Push(temp.Pop());

            this.currentLocationDialogue = newDialogue;

            if (newDialogue != null)
            {
                this.dialoguePushTime = Game1.timeOfDay;
                this.StateMachine.Companion.CurrentDialogue.Push(newDialogue); // Push new location dialogue
                this.monitor.Log($"New location dialogue pushed to {this.StateMachine.Name}'s stack");
            }
        }

        public bool PerformAction(Farmer who, GameLocation location)
        {
            if (this.ai != null && this.ai.PerformAction())
                return true;

            string[] answers = { "bag", "dismiss", "nothing" };

            this.StateMachine.CompanionManager.netEvents.FireEvent(new QuestionEvent("recruitedWant", this.StateMachine.Companion, answers), this.setByWhom);
            
            return true;
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
