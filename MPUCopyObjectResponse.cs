public class MPUCopyObjectResponse
{
    private readonly string _bucket =string.Empty;
    private readonly string _key=string.Empty;
    private string _message=string.Empty;
    public bool CopiedSuccessfully {get;set;}

    public MPUCopyObjectResponse(string bucket, string key)
    {
        _bucket=bucket;
        _key=key;
    }
    public string Bucket
    {
        get{return _bucket;}
    }
    public string Key
    {
        get{return _key;}
    }
    public string Message
    {
        get{return _message;}
        set{_message=value;}
    }
}