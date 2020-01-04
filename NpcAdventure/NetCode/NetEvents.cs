using NpcAdventure.AI.Controller;
using NpcAdventure.Loader;
using NpcAdventure.StateMachine;
using NpcAdventure.StateMachine.State;
using NpcAdventure.StateMachine.StateFeatures;
using NpcAdventure.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static NpcAdventure.AI.AI_StateMachine;
using static NpcAdventure.StateMachine.CompanionStateMachine;

namespace NpcAdventure.NetCode
{
    class NetEvents
    {
        private CompanionManager companionManager;

        private IMultiplayerHelper helper;

        private Dictionary<String, NetEventProcessor> messages;

        private IManifest modManifest;

        public NetEvents(IMultiplayerHelper helper)
        {
            this.helper = helper;

        }

        public void SetUp(IManifest manifest, CompanionManager manager, ContentLoader loader)
        {
            this.companionManager = manager;
            this.modManifest = manifest;

            this.messages = new Dictionary<string, NetEventProcessor>()
            {
                {DialogEvent.EVENTNAME, new NetEventShowDialogue(manager)},
                {CompanionRequestEvent.EVENTNAME, new NetEventRecruitNPC(manager)},
                {PlayerWarpedEvent.EVENTNAME, new NetEventPlayerWarped(manager)},
                {DialogueRequestEvent.EVENTNAME, new NetEventDialogueRequest(manager)},
                {QuestionEvent.EVENTNAME, new NetEventQuestionRequest(manager, this)},
                {QuestionResponse.EVENTNAME, new NetEventQuestionResponse(manager)},
                {CompanionChangedState.EVENTNAME, new NetEventCompanionChangedState(manager)},
                {CompanionStateRequest.EVENTNAME, new NetEventCompanionStateRequest(manager, this)},
                {CompanionDismissEvent.EVENTNAME, new NetEventDismissNPC(manager)},
                {SendChestEvent.EVENTNAME, new NetEventChestSent(manager)},
                {AIChangeState.EVENTNAME, new NetEventAIChangeState(manager)},
                {ShowHUDMessageHealed.EVENTNAME, new NetEventHUDMessageHealed(manager, loader)},
                {CompanionAttackAnimation.EVENTNAME, new NetEventCompanionAttackAnimation(manager)},
            };
        }

        private abstract class NetEventProcessor
        {
            public abstract void Process(NpcSyncEvent myEvent, Farmer owner);
            public abstract NpcSyncEvent Decode(ModMessageReceivedEventArgs e);
        }

        private class NetEventShowDialogue : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventShowDialogue(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override void Process(NpcSyncEvent npcEvent, Farmer owner)
            {
                DialogEvent dialogEvent = (DialogEvent)npcEvent;
                this.manager.PossibleCompanions[dialogEvent.otherNpc].currentState.ShowDialogue(DialogueHelper.GetSpecificDialogueText(this.manager.PossibleCompanions[dialogEvent.otherNpc].Companion, owner, dialogEvent.Dialog));
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                DialogEvent myEvent = e.ReadAs<DialogEvent>();
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

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionRequestEvent reqEvent = (CompanionRequestEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[reqEvent.otherNpc].currentState;
                if (n is AvailableState availableState)
                {
                    availableState.Recruit(owner);
                }
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionRequestEvent>();
            }
        }

        private class NetEventDismissNPC : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventDismissNPC(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionDismissEvent cde = (CompanionDismissEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[cde.otherNpc].currentState;
                if (n is RecruitedState rs)
                {
                    rs.StateMachine.Dismiss(owner, true);
                }
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionDismissEvent>();
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

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                PlayerWarpedEvent pwe = (PlayerWarpedEvent)myEvent;
                ICompanionState n = this.manager.PossibleCompanions[pwe.npc].currentState;
                if (n is RecruitedState recruitedState && n.GetByWhom().uniqueMultiplayerID == owner.uniqueMultiplayerID)
                {
                    NpcAdventureMod.GameMonitor.Log("Dispatching player warped to a recruited state...");

                    GameLocation from = Game1.getLocationFromName(pwe.warpedFrom);
                    GameLocation to = Game1.getLocationFromName(pwe.warpedTo);
                    recruitedState.PlayerHasWarped(from, to);
                }
            }
        }

        private class NetEventDialogueRequest : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventDialogueRequest(CompanionManager manager)
            {
                this.manager = manager;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<DialogueRequestEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                DialogueRequestEvent dre = (DialogueRequestEvent)myEvent;
                (this.manager.PossibleCompanions[dre.npc].currentState as IActionPerformer).PerformAction(owner, owner.currentLocation);
            }
        }

        private class NetEventQuestionRequest : NetEventProcessor
        {
            private CompanionManager manager;
            private NetEvents netbus;

            public NetEventQuestionRequest(CompanionManager manager, NetEvents events)
            {
                this.manager = manager;
                this.netbus = events;
            }
            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<QuestionEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                QuestionEvent qe = (QuestionEvent)myEvent;
                Response[] responses = new Response[qe.answers.Length];
                for (int i = 0; i < qe.answers.Length; i++)
                {
                    responses[i] = new Response(qe.answers[i], qe.answers[i]);
                }

                NPC n = this.manager.PossibleCompanions[qe.npc].Companion;

                owner.currentLocation.createQuestionDialogue(qe.question, responses, (_, answer) => {
                    this.netbus.FireEvent(new QuestionResponse(qe.question, answer, n), owner);
                }, n);
            }
        }

        private class NetEventQuestionResponse : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventQuestionResponse(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<QuestionResponse>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                QuestionResponse qr = (QuestionResponse)myEvent;

                IDialogueDetector detector = this.manager.PossibleCompanions[qr.npc].currentState as IDialogueDetector;
                if (detector != null) {
                    detector.OnDialogueSpeaked(qr.question, qr.response);
                }
            }
        }

        private class NetEventCompanionStateRequest : NetEventProcessor
        {
            private CompanionManager manager;
            private NetEvents netBus;

            public NetEventCompanionStateRequest(CompanionManager manager, NetEvents netBus)
            {
                this.manager = manager;
                this.netBus = netBus;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionStateRequest>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                foreach (var csmkv in this.manager.PossibleCompanions)
                {
                    this.netBus.FireEvent(new CompanionChangedState(csmkv.Value.Companion, csmkv.Value.CurrentStateFlag, csmkv.Value.currentState.GetByWhom()), owner);
                }
            }
        }

        private class NetEventAIChangeState : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventAIChangeState(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<AIChangeState>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                AIChangeState aics = (AIChangeState)myEvent;
                RecruitedState state = this.manager.PossibleCompanions[aics.npc].currentState as RecruitedState;
                if (state != null)
                {
                    state.ai.ChangeStateLocal(aics.newState);
                }
            }
        }

        private class NetEventHUDMessageHealed : NetEventProcessor
        {
            private CompanionManager manager;
            private ContentLoader loader;
            public NetEventHUDMessageHealed(CompanionManager manager, ContentLoader loader)
            {
                this.manager = manager;
                this.loader = loader;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<ShowHUDMessageHealed>();
            }
            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                ShowHUDMessageHealed message = myEvent as ShowHUDMessageHealed;
                Game1.addHUDMessage(new HUDMessage(this.loader.LoadString($"Strings/Strings:{message.message}", this.manager.PossibleCompanions[message.npc].Companion.displayName, message.health)));
            }
        }

        private class NetEventChestSent : NetEventProcessor
        {
            private CompanionManager manager;
            public NetEventChestSent(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<SendChestEvent>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                SendChestEvent sce = myEvent as SendChestEvent;
                MemoryStream stream = new MemoryStream();
                byte[] data = Convert.FromBase64String(sce.chestContents);
                stream.Write(data, 0, data.Length);
                stream.Seek(0, SeekOrigin.Begin);
                BinaryReader reader = new BinaryReader(stream);
                this.manager.PossibleCompanions[sce.npc].Bag.NetFields.ReadFull(reader, new Netcode.NetVersion());
                reader.Dispose();
                NpcAdventureMod.GameMonitor.Log("Received bag of " + sce.npc + " with " + this.manager.PossibleCompanions[sce.npc].Bag.items.Count + " items");
                foreach (Item item in this.manager.PossibleCompanions[sce.npc].Bag.items)
                {
                    NpcAdventureMod.GameMonitor.Log("Item " + item.Name + " #" + item.Stack);
                }
            }
        }

        private class NetEventCompanionAttackAnimation : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventCompanionAttackAnimation(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionAttackAnimation>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionAttackAnimation caa = (CompanionAttackAnimation)myEvent;
                RecruitedState rs = manager.PossibleCompanions[caa.npc].currentState as RecruitedState;
                if (rs != null)
                {
                    FightController fc = rs.ai.CurrentController as FightController;
                    if (fc != null)
                    {
                        //fc.AnimateMeLocal(caa.x, caa.y, caa.direction);
                        Character character = Game1.getCharacterFromName(caa.npc, true);
                        string posIsNull = (character == null ? "yes" : "no");
                        NpcAdventureMod.GameMonitor.Log("character " + caa.npc + " is null ? " + posIsNull + " am I a master?" + (Context.IsMainPlayer ? "yes" : "no"));
                        fc.AnimateMeLocal(character.Position.X, character.Position.Y, caa.direction);
                    }
                }
            }
        }

        private class NetEventCompanionChangedState : NetEventProcessor
        {
            private CompanionManager manager;

            public NetEventCompanionChangedState(CompanionManager manager)
            {
                this.manager = manager;
            }

            public override NpcSyncEvent Decode(ModMessageReceivedEventArgs e)
            {
                return e.ReadAs<CompanionChangedState>();
            }

            public override void Process(NpcSyncEvent myEvent, Farmer owner)
            {
                CompanionChangedState ccs = (CompanionChangedState)myEvent;
                CompanionStateMachine n = manager.PossibleCompanions[ccs.npc];
                switch (ccs.NewState)
                {
                    case StateFlag.AVAILABLE:
                        n.MakeLocalAvailable(ccs.GetByWhom());
                        break;
                    case StateFlag.RECRUITED:
                        n.RecruitLocally(ccs.GetByWhom());
                        break;
                    case StateFlag.RESET:
                        n.ResetLocalStateMachine(ccs.GetByWhom());
                        break;
                    case StateFlag.UNAVAILABLE:
                        n.MakeLocalUnavailable(ccs.GetByWhom());
                        break;
                    default:

                        break;
                }

                if (!Context.IsMainPlayer)
                    this.manager.ReinitializeNPCs();

            }
        }

        public void Register(IModEvents events)
        {
            events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
            events.Specialized.LoadStageChanged += this.OnLoadStageChanged;
        }

        private void OnLoadStageChanged(object sender, LoadStageChangedEventArgs e)
        {
            if (Context.IsMultiplayer && e.NewStage == StardewModdingAPI.Enums.LoadStage.Ready && !Game1.IsMasterGame)
            {
                this.FireEvent(new CompanionStateRequest());
            }
        }

        public void FireEvent(NpcSyncEvent myEvent, Farmer toWhom = null, bool isBroadcast = false) {
            if (isBroadcast)
            {
                foreach (Farmer farmer in Game1.getOnlineFarmers())
                {
                    if (farmer != Game1.player) // don't fire to the current player as it overwrites some fields as it is not a copy of the message
                        FireEvent(myEvent, farmer);
                }

                FireEvent(myEvent, Game1.player);

                return;
            }

            if (toWhom == null)
            {
                toWhom = Game1.MasterPlayer;
            }

            if (Context.IsMultiplayer && toWhom != Game1.player)
            {
                NpcAdventureMod.GameMonitor.Log("Sending message " + myEvent.Name + " to network", LogLevel.Info);
                helper.SendMessage<NpcSyncEvent>(myEvent, myEvent.Name, new string[] { this.modManifest.UniqueID }, new long[] { toWhom.uniqueMultiplayerID });
            }
            else
            {
                NpcAdventureMod.GameMonitor.Log("Delivering message" + myEvent.Name);
                NetEventProcessor eventProcessor = this.messages[myEvent.Name];
                eventProcessor.Process(myEvent, Game1.player);
            }
        }

        private void OnMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            NetEventProcessor processor = messages[e.Type];
            NpcSyncEvent npcEvent = processor.Decode(e);
            Farmer owner = Game1.getFarmer(e.FromPlayerID);
            NpcAdventureMod.GameMonitor.Log("Received message " + npcEvent.Name + " from " + owner.Name, LogLevel.Info);
            processor.Process(npcEvent, owner);
        }

        public abstract class NpcSyncEvent
        {
            public String Name;

            public NpcSyncEvent() { }

            public NpcSyncEvent(String Name)
            {
                this.Name = Name;
            }

        }

        public class DialogueRequestEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "dialogueRequestEvent";

            public string npc;
            public DialogueRequestEvent(NPC n) : base(EVENTNAME)
            {
                this.npc = n.Name;
            }
        }

        public class DialogEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "showDialogue";

            public string Dialog;
            public string otherNpc;

            public DialogEvent() { }

            public DialogEvent(string dialog, NPC otherNpc) : base(EVENTNAME)
            {
                this.Dialog = dialog;
                this.otherNpc = otherNpc.Name;
            }
        }

        public class QuestionEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "questionRequest";

            public string[] answers;
            public string question;
            public string npc;

            public QuestionEvent() : base()
            {

            }

            public QuestionEvent(string dialog, NPC otherNpc, string[] answers) : base(EVENTNAME)
            {
                this.question = dialog;
                this.npc = otherNpc.Name;
                this.answers = answers;
            }
        }

        public class QuestionResponse : NpcSyncEvent
        {
            public const string EVENTNAME = "questionResponse";
            public string question;
            public string response;
            public string npc;
            public QuestionResponse()
            {

            }

            public QuestionResponse(string question, string response, NPC n) : base(EVENTNAME)
            {
                this.question = question;
                this.npc = n.Name;
                this.response = response;
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

        public class CompanionDismissEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "companionDismiss";
            public string otherNpc;

            public CompanionDismissEvent() { }

            public CompanionDismissEvent(NPC otherNpc) : base(EVENTNAME)
            {
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

        public class CompanionChangedState : NpcSyncEvent
        {
            public const string EVENTNAME = "companionChangedState";
            public StateFlag NewState;
            public long byWhom;
            public string npc;

            public Farmer GetByWhom()
            {
                if (this.byWhom == 0)
                    return null;

                return Game1.getFarmer(this.byWhom);
            }

            public CompanionChangedState() { }

            public CompanionChangedState(NPC n, StateFlag NewState, Farmer byWhom) : base(EVENTNAME)
            {
                this.npc = n.Name;
                this.NewState = NewState;
                if (byWhom != null)
                    this.byWhom = byWhom.uniqueMultiplayerID;
            }
        }

        public class CompanionStateRequest : NpcSyncEvent
        {
            public const string EVENTNAME = "companionStateRequest";

            public CompanionStateRequest() : base(EVENTNAME) { }

        }

        public class SendChestEvent : NpcSyncEvent
        {
            public const string EVENTNAME = "sendChestEvent";
            public string npc;
            public string chestContents;

            public SendChestEvent() { }

            public SendChestEvent(NPC n, Chest chest) : base(EVENTNAME)
            {
                MemoryStream stream = new MemoryStream();
                BinaryWriter writer = new BinaryWriter(stream);
                chest.NetFields.WriteFull(writer);
                writer.Dispose();
                this.chestContents = Convert.ToBase64String(stream.ToArray());

                NpcAdventureMod.GameMonitor.Log("Trying to write " + chestContents);
                this.npc = n.Name;
            }
        }

        public class AIChangeState : NpcSyncEvent
        {
            public const string EVENTNAME = "aiChangeState";

            public string npc;
            public State newState;

            public AIChangeState() { }

            public AIChangeState(NPC n, State newState) : base(EVENTNAME)
            {
                this.npc = n.Name;
                this.newState = newState;
            }
        }

        public abstract class ShowHUDMessage : NpcSyncEvent
        { 
            public string npc;
            public string message;
            public int type;

            public ShowHUDMessage() { }

            public ShowHUDMessage(string eventname, NPC n, string message, int type) : base(eventname)
            {
                this.npc = n.Name;
                this.message = message;
                this.type = type;
            }

            public abstract object[] gatherArgs();

        }

        public class ShowHUDMessageHealed : ShowHUDMessage
        {
            public const string EVENTNAME = "showHudMessageHealed";

            public int health;

            public ShowHUDMessageHealed() { }

            public ShowHUDMessageHealed(NPC n, int health) : base(EVENTNAME, n, "healed", HUDMessage.health_type)
            {
                this.health = health;
            }

            public override object[] gatherArgs()
            {
                return new object[] { this.npc, this.health };
            }
        }

        public class CompanionAttackAnimation : NpcSyncEvent
        {
            public const string EVENTNAME = "companionAttackAnimation";

            public string npc;
            public float x;
            public float y;
            public int direction;

            public CompanionAttackAnimation() { }

            public CompanionAttackAnimation(NPC n, float x, float y, int direction) : base(EVENTNAME)
            {
                this.npc = n.Name;
                this.x = x;
                this.y = y;
                this.direction = direction;
            }
        }
    }
}
