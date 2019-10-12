using System;
using System.Collections.Generic;
using System.Text;

namespace LibZeroTier
{
    class LibZeroTierException : Exception
    {
        public LibZeroTierException()
        {
        }
        public LibZeroTierException(string message)
            : base(message)
        {
        }

        public LibZeroTierException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
