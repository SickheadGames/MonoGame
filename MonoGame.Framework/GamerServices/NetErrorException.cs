using System;

namespace Microsoft.Xna.Framework.GamerServices
{
    public class NetErrorException : Exception
    {
        public MonoGame.Switch.UserId UserId { get; private set; }
        public int Code { get; private set; }
        public int Category { get; private set; }

        public NetErrorException(MonoGame.Switch.UserId userId, int code, int category)
        {
            UserId = userId;
            Code = code;
            Category = category;
        }

        public override string ToString()
        {
            return string.Format("NetErrorException; UserId={0}, Code={1:x6}, Category={2}\n", UserId, Code, Category) + StackTrace;
        }
    }
}
