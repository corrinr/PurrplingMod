using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NpcAdventure.StateMachine.State
{
    class UnavailableState : CompanionState
    {
        public UnavailableState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor) : base(stateMachine, events, monitor)
        {
        }

        public override void Entry(Farmer byWhom)
        {
            this.setByWhom = byWhom;
        }
    }
}
