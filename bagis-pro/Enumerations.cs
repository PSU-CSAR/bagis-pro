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
        SITES_ALL,
        SNODAS_SWE,
        PRISM
    }

    public enum SiteType
    {
        Missing,
        Snotel,
        SnowCourse,
        Pseudo,
        SitesAll
    }

    public enum PrismFile
    {
        Jan,
        Feb,
        Mar,
        Apr,
        May,
        Jun,
        Jul,
        Aug,
        Sep,
        Oct,
        Nov,
        Dec,
        Q1,
        Q2,
        Q3,
        Q4,
        Annual
    }
}
