using NpcAdventure.Utils;
using NpcAdventure.StateMachine.StateFeatures;
using StardewModdingAPI.Events;
using StardewValley;
using StardewModdingAPI;
using System.Collections.Generic;
using System;
using static NpcAdventure.NetCode.NetEvents;

namespace NpcAdventure.StateMachine.State
{
    internal class AvailableState : CompanionState, IRequestedDialogueCreator, IDialogueDetector
    {
        private Dialogue acceptalDialogue;
        private Dialogue suggestionDialogue;

        public bool CanCreateDialogue { get; private set; }

        private int doNotAskUntil;

        public AvailableState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor) : base(stateMachine, events, monitor) {}

        public override void Entry(Farmer byWhom)
        {
            this.setByWhom = byWhom;
            this.CanCreateDialogue = true;
            this.Events.GameLoop.TimeChanged += this.GameLoop_TimeChanged;
        }

        private void GameLoop_TimeChanged(object sender, TimeChangedEventArgs e)
        {
            if (this.CanCreateDialogue == false && e.NewTime > this.doNotAskUntil)
                this.CanCreateDialogue = true;

            int heartLevel = this.StateMachine.CompanionManager.Farmer.getFriendshipHeartLevelForNPC(this.StateMachine.Companion.Name);

            if (this.CanCreateDialogue && e.NewTime < 2200 && this.suggestionDialogue == null && heartLevel > 4 && Game1.random.NextDouble() < this.GetSuggestChance())
            {
                Dialogue d = DialogueHelper.GenerateDialogue(this.StateMachine.Companion, "companionSuggest");

                if (d == null)
                    return; // No dialogue defined, nothing to suggest

                // Add reaction on adventure suggestion acceptance/rejectance question
                d.answerQuestionBehavior = new Dialogue.onAnswerQuestion((whichResponse) => {
                    List<Response> opts = d.getResponseOptions();
                    NPC n = this.StateMachine.Companion;

                    if (opts[whichResponse].responseKey == "Yes")
                    {
                        // Farmer accepted suggestion of adventure. Let's go to find a some trouble!
                        this.acceptalDialogue = new Dialogue(DialogueHelper.GetDialogueString(n, "companionSuggest_Yes"), n);
                        DialogueHelper.DrawDialogue(this.acceptalDialogue);
                    } else
                    {
                        // Farmer not accepted for this time. Farmer can't ask to follow next 2 hours
                        this.CanCreateDialogue = false;
                        this.doNotAskUntil = e.NewTime + 200;
                        DialogueHelper.DrawDialogue(new Dialogue(DialogueHelper.GetDialogueString(n, "companionSuggest_No"), n));
                    }

                    this.suggestionDialogue = null;
                    return false;
                });
                this.suggestionDialogue = d;
                this.StateMachine.Companion.CurrentDialogue.Push(d);
                this.monitor.Log($"Added adventure suggest dialogue to {this.StateMachine.Companion.Name}");
            } else if (this.suggestionDialogue != null)
            {
                if (e.NewTime >= 2200 || heartLevel <= 4)
                {
                    // Remove suggestion dialogue when it'S over 22:00 or friendship heart level decreased under recruit heart threshold
                    DialogueHelper.RemoveDialogueFromStack(this.StateMachine.Companion, this.suggestionDialogue);
                    this.suggestionDialogue = null;
                    this.monitor.Log($"Removed adventure suggest dialogue from {this.StateMachine.Companion.Name}");
                }
            }
        }

        private float GetSuggestChance()
        {
            int heartLevel = this.StateMachine.CompanionManager.Farmer.getFriendshipHeartLevelForNPC(this.StateMachine.Companion.Name);
            bool married = Helper.IsSpouseMarriedToFarmer(this.StateMachine.Companion, this.StateMachine.CompanionManager.Farmer);
            float chance = 0.066f * heartLevel;

            if (married)
                chance /= 2;

            return chance;
        }

        public override void Exit()
        {
            this.Events.GameLoop.TimeChanged -= this.GameLoop_TimeChanged;
            this.CanCreateDialogue = false;
            this.suggestionDialogue = null;
        }

        public void Recruit(Farmer byWhom)
        {
            if (!this.StateMachine.Companion.doingEndOfRouteAnimation.Value)
            {
                this.StateMachine.Companion.Halt();
                this.StateMachine.Companion.facePlayer(byWhom);
            }
            this.ReactOnAnswer(this.StateMachine.Companion, byWhom);
        }

        private void ReactOnAnswer(NPC n, Farmer byWhom)
        {
            if (this.StateMachine.CompanionManager.PossibleCompanions[n.Name].CurrentStateFlag != CompanionStateMachine.StateFlag.AVAILABLE) // make sure they're not taken
            {
                this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent("companionTaken", n));
                return;
            }

            foreach (var csmKv in this.StateMachine.CompanionManager.PossibleCompanions)
            {
                if (byWhom.uniqueMultiplayerID == csmKv.Value.currentState.GetByWhom().uniqueMultiplayerID && csmKv.Value.CurrentStateFlag != CompanionStateMachine.StateFlag.RECRUITED) // if the person is already taken by us
                {
                    this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent("companionYoureNotFree", n));
                    return;
                } 
                else if (byWhom.uniqueMultiplayerID == csmKv.Value.currentState.GetByWhom().uniqueMultiplayerID && csmKv.Value.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED) // HACK remove when we get rclick event syncing
                {
                    this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent( "recruitedWant", n));
                }
            }

            if (byWhom.getFriendshipHeartLevelForNPC(n.Name) < this.StateMachine.CompanionManager.Config.HeartThreshold || Game1.timeOfDay >= 2200)
            {
                this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent(Game1.timeOfDay >= 2200 ? "companionRejectedNight" : "companionRejected", n), byWhom);
                this.StateMachine.MakeUnavailable(byWhom);
            }
            else
            {
                this.StateMachine.CompanionManager.netEvents.FireEvent(new DialogEvent("companionAccepted", n), byWhom);

                this.StateMachine.CompanionManager.Farmer.changeFriendship(40, this.StateMachine.Companion);
                this.StateMachine.Recruit(byWhom);
            }
        }

        public void CreateRequestedDialogue(string receivedAnswer = null)
        {
            Farmer leader = this.StateMachine.CompanionManager.Farmer;
            NPC companion = this.StateMachine.Companion;
            GameLocation location = this.StateMachine.CompanionManager.Farmer.currentLocation;
            string question = this.StateMachine.ContentLoader.LoadString("Strings/Strings:askToFollow", companion.displayName);

            location.createQuestionDialogue(question, location.createYesNoResponses(), (_, answer) =>
            {
                if (answer == "Yes")
                {
                    this.StateMachine.CompanionManager.netEvents.FireEvent(new CompanionRequestEvent(this.StateMachine.Companion));
                }
            }, null);
        }

        public void OnDialogueSpeaked(string question, string answer) // XXXX taky jenom na serveru
        {

        }
    }
}
