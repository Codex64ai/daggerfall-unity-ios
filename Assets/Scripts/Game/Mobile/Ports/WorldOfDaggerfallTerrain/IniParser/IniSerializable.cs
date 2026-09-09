// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/IniParser/IniSerializable.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using IniParser.Model;

public interface IniSerializable
{
    string[] GetSerializedSection(string comment = "");
    void DeserializeSection(string sectionName, KeyDataCollection keyData);
}
