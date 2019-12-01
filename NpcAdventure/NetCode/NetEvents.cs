using NpcAdventure.StateMachine;
using NpcAdventure.StateMachine.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;

namespace NpcAdventure.NetCode
{
    class NetEvents
    {
        private CompanionManager companionManager;

        private IMultiplayerHelper helper;

        private Dictionary<String, NetEventProcessor> messages;

        public NetEvents(IMultiplayerHelper helper)
        {
            this.helper = helper;
            
        }

        public void SetUp(CompanionManager manager)
        {
            this.messages = new Dictionary<string, NetEventProcessor>()
            {
                {"showDialogue", new NetEventShowDialogue(manager)},
                {"companionRequest", new NetEventRecruitNPC(manager)},
                {PlayerWarpedEvent.EVENTNAME, new NetEventPlayerWarped(manager)},
            };
        }

        private abstract class NetEventProcessor
        {
            public abstract NpcSyncEvent Process(NpcSyncEvent myEvent);
            public abstract NpcSyncEvent Decode(ModMessageReceivedEventArgs e);
        }

        private class NetEventShowDialogue : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventShowDialogue(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override NpcSyncEvent Process(NpcSyncEvent npcEvent)
            {
                DialogEvent dialogEvent = (DialogEvent)npcEvent;
                this.manager.PossibleCompanions[dialogEvent.otherNpc].currentState.ShowDialogue(dialogEvent.Dialog);

                return null;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                DialogEvent myEvent = e.ReadAs<DialogEvent>();
                myEvent.owner = Game1.getFarmer(e.FromPlayerID);
                return myEvent;
            }
        }

        private class NetEventRecruitNPC : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventRecruitNPC(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Process(NpcSyncEvent myEvent)
            {
                CompanionRequestEvent reqEvent = (CompanionRequestEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[reqEvent.otherNpc].currentState;
                if (n is AvailableState availableState)
                {
                    availableState.Recruit(reqEvent.owner);
                }

                return null;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionRequestEvent>();
            }
        }

        private class NetEventPlayerWarped : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventPlayerWarped(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<PlayerWarpedEvent>();
            }

            public override NpcSyncEvent Process(NpcSyncEvent myEvent)
            {
                PlayerWarpedEvent pwe = (PlayerWarpedEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[pwe.npc].currentState;
                if(n is RecruitedState recruitedState)
                {
                    GameLocation from = Game1.getLocationFromName(pwe.warpedFrom);
                    GameLocation to = Game1.getLocationFromName(pwe.warpedTo);
                    recruitedState.PlayerHasWarped(from, to);
                }

                return null;
            }
        }

        public void Register(IModEvents events)
        {
            events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
        }

        public void FireEvent(NpcSyncEvent myEvent, Farmer toWhom = null) {
            if (toWhom == null)
            {
                toWhom = Game1.MasterPlayer;
            }

            if (Context.IsMultiplayer && toWhom != Game1.player)
            {
                NpcAdventureMod.GameMonitor.Log("Sending message " + myEvent.Name + " to network", LogLevel.Info);
                helper.SendMessage<NpcSyncEvent>(myEvent, myEvent.Name, new string[] { "purrplingcat.npcadventure"}, new long[] { toWhom.uniqueMultiplayerID });
            }
            else
            {
                NetEventProcessor eventProcessor = this.messages[myEvent.Name];
                myEvent.owner = Game1.MasterPlayer;
                eventProcessor.Process(myEvent);
            }
        }

        public void RegisterCompanionManager(CompanionManager companionManager)
        {
            this.companionManager = companionManager;
        }

        private void OnMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            NetEventProcessor processor = messages[e.Type];
            NpcSyncEvent npcEvent = processor.Decode(e);
            npcEvent.owner = Game1.getFarmer(e.FromPlayerID);
            NpcAdventureMod.GameMonitor.Log("Received message " + npcEvent.Name + " from " + npcEvent.owner.Name, LogLevel.Info);
            processor.Process(npcEvent);
        }

        public abstract class NpcSyncEvent
        {
            public String Name;
            public Farmer owner;

            public NpcSyncEvent() { }

            public NpcSyncEvent(String Name)
            {
                this.Name = Name;
            }

        }

        public class DialogEvent : NpcSyncEvent
        {
            public string Dialog;
            public string otherNpc;

            public DialogEvent() { }

            public DialogEvent(String name, string dialog, NPC otherNpc) : base(name)
            {
                this.Dialog = dialog;
                this.otherNpc = otherNpc.Name;
            }
        }


        public class CompanionRequestEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "companionRequest";
            public string otherNpc;

            public CompanionRequestEvent() { }
            public CompanionRequestEvent(NPC otherNpc) : base(EVENTNAME) {
                this.otherNpc = otherNpc.Name;
            }

        }

        public class PlayerWarpedEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "playerWarped";
            public string warpedFrom;
            public string warpedTo;
            public string npc;
            public PlayerWarpedEvent() { }

            public PlayerWarpedEvent(NPC n, GameLocation from, GameLocation to) : base(EVENTNAME)
            {
                this.warpedFrom = from.NameOrUniqueName;
                this.warpedTo = to.NameOrUniqueName;
                this.npc = n.Name;
            }
        }
        /*
        public class CompanionResultEvent : DialogEvent
        {
            public const string EVENTNAME = "companionResult";
            public CompanionResultEvent()
            {

            }

            public CompanionResultEvent(string result) : base(EVENTNAME,)
            {

            }

            public override void OnReceive(CompanionManager manager)
            {
                throw new NotImplementedException();
            }
        }*/
    }
}
