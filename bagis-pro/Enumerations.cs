using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bagis_pro
{
    public enum BA_ReturnCode
    {
        Success,
        UnknownError,
        NotSupportedOperation,
        ReadError,
        WriteError,
        OtherError
    }
}
