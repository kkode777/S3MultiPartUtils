public class Metadata
{
    private Dictionary<string,string> _metaDataCollection=new Dictionary<string, string>();
    public bool StrippedKeysPresent{get;set;}
    public long ContentLength{get;set;}

    public string MetadataError{get;set;}=string.Empty;

    public Dictionary<string,string> MetadataCollection
    {
        get{return _metaDataCollection;}
        set{_metaDataCollection=value;}
    }
}