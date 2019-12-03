using NpcAdventure.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NpcAdventure.NetCode.NetEvents;

namespace NpcAdventure.StateMachine
{
    public interface ICompanionState
    {
        /// <summary>
        /// Enter to this state
        /// </summary>
        void Entry(Farmer byWhom);
        
        /// <summary>
        /// Exit from this state
        /// </summary>
        void Exit();

        /// <summary>
        /// Show a dialogue with the companion
        /// </summary>
        /// <param name="message">Message that was received</param>
        void ShowDialogue(string message);

        Farmer GetByWhom();
    }

    internal abstract class CompanionState : ICompanionState
    {
        public CompanionStateMachine StateMachine { get; private set; }
        protected IModEvents Events { get; }

        protected readonly IMonitor monitor;

        public Farmer setByWhom;

        public CompanionState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor)
        {
            this.StateMachine = stateMachine ?? throw new Exception("State Machine must be set!");
            this.Events = events;
            this.monitor = monitor;

            this.Events.Player.Warped += Player_Warped;
        }

        private void Player_Warped(object sender, WarpedEventArgs e) // HACK this should be moved to global place ONCE we get tracking per-companion per-player
        {
            if (e.Player != Game1.MasterPlayer) // send to the server when we change location and we're not server
            {
                if (this.StateMachine.Companion != null)
                {
                    this.StateMachine.CompanionManager.netEvents.FireEvent(new PlayerWarpedEvent(this.StateMachine.Companion, e.OldLocation, e.NewLocation));
                }
            }
        }

        public void ShowDialogue(string message)
        {
            DialogueHelper.DrawDialogue(new Dialogue(message, this.StateMachine.Companion));
        }

        public Farmer GetByWhom()
        {
            return this.setByWhom;
        }

        /// <summary>
        /// By default do nothing when this state entered
        /// </summary>
        public virtual void Entry(Farmer byWhom) {}

        /// <summary>
        /// By default do nothing when this state was exited
        /// </summary>
        public virtual void Exit() {}
    }
}
