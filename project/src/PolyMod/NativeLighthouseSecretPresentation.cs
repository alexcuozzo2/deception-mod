using System;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static class NativeLighthouseSecretPresentation
    {
        internal static void Complete(object action,object completion)
        {
            try
            {
                var intent=NativeExchangeCommands.Get(action);
                object? state=Native.Static("GameManager","GameState"),client=Native.Static("GameManager","Client");
                if(intent?.Kind!=NativeExchangeKind.LighthouseSecret||state is null||client is null||
                    !Native.Same(state,Native.Get(client,"GameState"))||!NativeRegroupSession.Ready(state))return;
                NativeExchangeCommands.ValidateBase(action,intent);
                var snapshot=NativeGameStateEnvelope.Get(state);
                if(snapshot is null)return;
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                if(ledger.Epoch!=intent.Epoch||!NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,intent))return;
                // This copies the committed treasury into LocalWallet and
                // refreshes its label. No income animation, map refresh or SFX.
                Native.Invoke(Native.M("ResourceEvents","RefreshWallets","System.Byte"),null,intent.Actor);
            }
            catch(Exception e){Entry.Note("lighthouse-wallet-refresh-error",e.Message);}
            finally{Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),completion);}
        }
    }
}
