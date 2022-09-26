/*
    The material embodied in this software is provided to you "as-is" and without warranty of any kind, express, implied or otherwise, including without limitation, any warranty of fitness for a particular purpose.
*/
using CsvHelper.Configuration;
public class InventoryItem
{
    
    public string ObjectName { get; set; }=string.Empty;
    public string SourceBucketName{get;set;}=string.Empty;
    public string TargetBucketName{get;set;}=string.Empty;
    public string FileOwner{get;set;}=string.Empty;
    public string FilePermissions{get;set;}=string.Empty;
    public string FileGroup{get;set;}=string.Empty;
    public string FileAcl{get;set;}=string.Empty;

}

public sealed class InventoryItemMap : ClassMap<InventoryItem>
{
    public InventoryItemMap()
    {
        Map(m => m.ObjectName).Name("object_name");
        Map(m => m.SourceBucketName).Name("source_bucket_name");
        Map(m=>m.TargetBucketName).Name("target_bucket_name");
        Map(m=>m.FileOwner).Name("file-owner");
        Map(m=>m.FilePermissions).Name("file-permissions");
        Map(m=>m.FileGroup).Name("file-group");
        Map(m=>m.FileAcl).Name("file-acl");
    }
}