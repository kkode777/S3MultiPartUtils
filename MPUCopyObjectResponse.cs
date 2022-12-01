/*
    The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose.
*/
public class MPUCopyObjectResponse
{
    private readonly string _sourceBucket =string.Empty;
    private readonly string _sourceKey=string.Empty;
    private readonly string _targetBucket =string.Empty;
    private readonly string _targetKey=string.Empty;
    private string  _strippedMetadata=string.Empty;
    private string _message=string.Empty;
    public bool CopiedSuccessfully {get;set;}

    private long _objectSize;
    private double _elapsedTimeInMinutes;

    public MPUCopyObjectResponse(string sourceBucket, string sourceKey, string targetBucket, string targetKey)
    {
        _sourceBucket=sourceBucket;
        _sourceKey=sourceKey;
        _targetBucket=targetBucket;
        _targetKey=targetKey;
    }
    public string SourceBucket
    {
        get{return _sourceBucket;}
    }
    public string SourceKey
    {
        get{return _sourceKey;}
    }
    public string TargetBucket
    {
        get{return _targetBucket;}
    }
    public string TargetKey
    {
        get{return _targetKey;}
    }

    public string StrippedMetadata
    {
        get{return _strippedMetadata;}
        set{_strippedMetadata=value;}
    }
    public string Message
    {
        get{return _message;}
        set{_message=value;}
    }

    public long ObjectSize
    {
        get{return _objectSize;}
        set{_objectSize=value;}
    }

    public double ElapsedTimeInMinutes
    {
        get{return _elapsedTimeInMinutes;}
        set{_elapsedTimeInMinutes=value;}
    }
}