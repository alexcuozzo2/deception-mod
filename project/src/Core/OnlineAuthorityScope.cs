using System;
using System.IO;

namespace Deception.Core
{
    // A bounded lifetime check, not a source of lobby or gameplay authority.
    // The adapter supplies its own stable state token after native admission;
    // a recreated managed wrapper is not automatically the same token.
    public sealed class OnlineAuthorityScope:IDisposable
    {
        readonly object StateToken;
        public Guid Epoch { get; }
        public int OwnerThread { get; }
        public bool IsClosed { get; private set; }
        public OnlineAuthorityScope(Guid epoch,object stableStateToken)
        {
            ArgumentNullException.ThrowIfNull(stableStateToken);
            if(epoch==Guid.Empty)throw new InvalidDataException("Online authority requires an assigned native match Guid.");
            Epoch=epoch;StateToken=stableStateToken;OwnerThread=Environment.CurrentManagedThreadId;
        }
        void RequireThread()
        {
            if(Environment.CurrentManagedThreadId!=OwnerThread)
                throw new InvalidDataException("Online authority crossed its owning thread.");
        }
        public void Require(Guid epoch,object stableStateToken)
        {
            RequireThread();
            if(IsClosed||epoch!=Epoch||!ReferenceEquals(stableStateToken,StateToken))
                throw new InvalidDataException("Online authority is closed or changed match/state.");
        }
        public void Dispose()
        {
            RequireThread();IsClosed=true;
        }
    }
}
