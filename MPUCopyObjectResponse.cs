public class MPUCopyObjectResponse
{
    private string _message=string.Empty;
    public bool CopiedSuccessfully {get;set;}
    public string Message
    {
        get{return _message;}
        set{_message=value;}
    }
}