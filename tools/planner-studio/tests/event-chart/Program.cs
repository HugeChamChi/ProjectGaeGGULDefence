using System;
using System.IO;
using LitJson;
using GGD.BackendCharts;
var rows=File.ReadAllText(args[0]);
var expected=File.ReadAllText(args[1]);
if(ChartSnapshotPayload.Decode(rows)!=expected) throw new Exception("Roundtrip mismatch");
void Reject(string text) {try {ChartSnapshotPayload.Decode(text);}catch(ArgumentException){return;}throw new Exception("Expected rejection");}
var bad=JsonMapper.ToObject(rows);bad[0]["Sha256"]="wrong";Reject(bad.ToJson());
bad=JsonMapper.ToObject(rows);bad[0]["PartCount"]="999";Reject(bad.ToJson());
bad=JsonMapper.ToObject(rows);bad[0]["SchemaVersion"]="2";Reject(bad.ToJson());
Reject("[]");
Console.WriteLine("PASS event chart payload roundtrip/checksum/count/version/empty");
