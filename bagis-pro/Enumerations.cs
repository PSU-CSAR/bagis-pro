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
        PRISM,
        ROADS,
        PUBLIC_LAND_ZONES,
        BELOW_TREELINE,
        SITES_LOCATION,
        SITES_LOCATION_PRECIP,
        SITES_LOCATION_PRECIP_CONTRIB,
        CRITICAL_PRECIP,
        PUBLIC_LAND_OWNERSHIP,
        PRECIPITATION_CONTRIBUTION,
        WINTER_PRECIPITATION,
        AOI_LOCATION
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

    public enum PrismServiceNames
    {
        Prism_Precipitation_01,
        Prism_Precipitation_02,
        Prism_Precipitation_03,
        Prism_Precipitation_04,
        Prism_Precipitation_05,
        Prism_Precipitation_06,
        Prism_Precipitation_07,
        Prism_Precipitation_08,
        Prism_Precipitation_09,
        Prism_Precipitation_10,
        Prism_Precipitation_11,
        Prism_Precipitation_12,
        Prism_Precipitation_q1,
        Prism_Precipitation_q2,
        Prism_Precipitation_q3,
        Prism_Precipitation_q4,
        Prism_Precipitation_annual
    }

    public enum SeasonalPrismFile
    {
        Sq1,
        Sq2,
        Sq3,
        Sq4
    }

    public enum SeasonalPrismServiceNames
    {
        Prism_Precipitation_sq1,
        Prism_Precipitation_sq2,
        Prism_Precipitation_sq3,
        Prism_Precipitation_sq4,
    }

    public enum AoiBatchState
    {
        Waiting,
        Started,
        Failed,
        Completed,
        Errors
    }

    public enum ReportType
    {
        Watershed,
        SiteAnalysis
    }

    public enum SiteProperties
    {
        Aspect,
        Precipitation
    }
}
