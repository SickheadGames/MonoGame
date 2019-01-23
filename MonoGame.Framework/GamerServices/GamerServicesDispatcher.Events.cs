
using System;

namespace Microsoft.Xna.Framework.GamerServices
{
    public static partial class GamerServicesDispatcher
    {
        private interface IGamerServicesEvent
        {
            void Dispatch();
        }

        private class GamerSignInEvent : IGamerServicesEvent
        {
            public object Sender { get; set; }
            public SignedInEventArgs Args { get; set; }

            public void Dispatch()
            {                
                var gamer = Args.Gamer;
                Gamer.SignedInGamers.Add(gamer);

                gamer.LeaderboardWriter = new LeaderboardWriter(gamer);

                Console.WriteLine("GamerSignInEvent.Dispatch(); Added gamer '{0}' at index '{1}'", gamer.DisplayName, gamer.PlayerIndex);                
                
                SignedInGamer.TriggerSignedIn(Sender, Args);
            }
        }

        private class GamerSignOutEvent : IGamerServicesEvent
        {
            public object Sender { get; set; }
            public SignedOutEventArgs Args { get; set; }

            public void Dispatch()
            {
                var gamer = Args.Gamer;
                Console.WriteLine("GamerSignInEvent.Dispatch(); Removing gamer '{0}' at index '{1}'", gamer.DisplayName, gamer.PlayerIndex);

                Gamer.SignedInGamers.Remove(gamer);
                
                SignedInGamer.TriggerSignedOut(null, new SignedOutEventArgs(gamer));
                
                gamer.Dispose();                
            }
        }
    }
}
