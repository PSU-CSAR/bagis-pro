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

    public enum FolderType
    {
        AOI,
        BASIN,
        FOLDER
    }

    public enum BagisMapType
    {
        ELEVATION,
        ASPECT,
        SLOPE,
        SNOTEL,
        SCOS,
        SITES_ALL
    }

    public enum SiteType
    {
        MISSING,
        SNOTEL,
        SNOW_COURSE,
        PSEUDO,
        SITES_ALL
    }
}
