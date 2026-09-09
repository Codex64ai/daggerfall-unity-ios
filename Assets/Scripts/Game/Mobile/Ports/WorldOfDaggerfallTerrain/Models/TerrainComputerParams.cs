// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/TerrainComputerParams.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using IniParser.Model;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static IniUtils;

namespace Monobelisk
{
#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "terrainComputerParams", menuName = "Monobelisk/Terrain Computer Params")]
#endif
    [System.Serializable]
    public class TerrainComputerParams
#if UNITY_EDITOR
        : ScriptableObject 
#endif
    {
        [SerializeField]
        public SwissTurbulence swissFolded = new SwissTurbulence("swissFolded");
        [SerializeField]
        public JordanTurbulence jordanFolded = new JordanTurbulence("jordanFolded");
        [SerializeField]
        public Perlin perlinTile = new Perlin("perlinTile");
        [SerializeField]
        public Perlin perlinBump = new Perlin("perlinBump");
        [SerializeField]
        public SwissTurbulence iqMountain = new SwissTurbulence("iqMountain");
        [SerializeField]
        public SwissTurbulence swissCell = new SwissTurbulence("swissCell");
        [SerializeField]
        public SwissTurbulence swissFaults = new SwissTurbulence("swissFaults");
        [SerializeField]
        public Perlin perlinDune = new Perlin("perlinDune");
        [SerializeField]
        public SwissTurbulence swissDune = new SwissTurbulence("swissDune");
        [SerializeField]
        public Perlin mntVar = new Perlin("mntVar");
        [SerializeField]
        public Perlin colorVar = new Perlin("colorVar");
        [SerializeField]
        public SwissTurbulence mountainBase = new SwissTurbulence("mountainBase");
        [SerializeField]
        public Perlin hillBase = new Perlin("hillBase");

        public void FromIniData(IniData iniDocument)
        {
            iniDocument.ClearAllComments();

            var section = iniDocument.Sections.GetSectionData("swissFolded");
            swissFolded.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("jordanFolded");
            jordanFolded.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("perlinTile");
            perlinTile.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("perlinBump");
            perlinBump.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("iqMountain");
            iqMountain.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("swissCell");
            swissCell.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("swissFaults");
            swissFaults.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("perlinDune");
            perlinDune.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("swissDune");
            swissDune.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("mntVar");
            mntVar.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("colorVar");
            colorVar.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("mountainBase");
            mountainBase.DeserializeSection(section.SectionName, section.Keys);

            section = iniDocument.Sections.GetSectionData("hillBase");
            hillBase.DeserializeSection(section.SectionName, section.Keys);
        }

        public string ToIni()
        {
            List<string> sections = new List<string>();

            sections.Add(swissFolded.GetSerializedSection("Main mountain generator")
                .Aggregate(NewLines));

            sections.Add(jordanFolded.GetSerializedSection("Hillscape generator")
                .Aggregate(NewLines));

            sections.Add(perlinTile.GetSerializedSection("Currently unused")
                .Aggregate(NewLines));

            sections.Add(perlinBump.GetSerializedSection("Flatness-preventing terrain bumps")
                .Aggregate(NewLines));

            sections.Add(iqMountain.GetSerializedSection("Occasional sharp hills")
                .Aggregate(NewLines));

            sections.Add(swissCell.GetSerializedSection("Currently unused")
                .Aggregate(NewLines));

            sections.Add(swissFaults.GetSerializedSection("Cliff/ravine/canyon generator")
                .Aggregate(NewLines));

            sections.Add(perlinDune.GetSerializedSection("Currently unused")
                .Aggregate(NewLines));

            sections.Add(swissDune.GetSerializedSection("Dunes for deserts")
                .Aggregate(NewLines));

            sections.Add(mntVar.GetSerializedSection("Mask used to alternate between mountains and cliffs")
                .Aggregate(NewLines));

            sections.Add(colorVar.GetSerializedSection("A 4-color noise sample of various frequencies, used for various purposes")
                .Aggregate(NewLines));

            sections.Add(mountainBase.GetSerializedSection("Add occasional mountains to non-montane regions")
                .Aggregate(NewLines));

            sections.Add(hillBase.GetSerializedSection("Add occasional hills to non-woodland hills regions")
                .Aggregate(NewLines));

            return sections.Aggregate((s1, s2) => s1 + "\n\n" + s2);
        }

        private string NewLines(string s1, string s2)
        {
            return s1 + "\n" + s2;
        }

        public void ApplyToCS(ComputeShader cs)
        {
            swissFolded.ApplyToCS(cs);
            jordanFolded.ApplyToCS(cs);
            perlinTile.ApplyToCS(cs);
            perlinBump.ApplyToCS(cs);
            iqMountain.ApplyToCS(cs);
            swissCell.ApplyToCS(cs);
            swissFaults.ApplyToCS(cs);
            perlinDune.ApplyToCS(cs);
            swissDune.ApplyToCS(cs);
            mntVar.ApplyToCS(cs);
            colorVar.ApplyToCS(cs);
            mountainBase.ApplyToCS(cs);
            hillBase.ApplyToCS(cs);
        }

        public void ApplyToMaterial(Material mat)
        {
            swissFolded.ApplyToMaterial(mat);
            jordanFolded.ApplyToMaterial(mat);
            perlinTile.ApplyToMaterial(mat);
            perlinBump.ApplyToMaterial(mat);
            iqMountain.ApplyToMaterial(mat);
            swissCell.ApplyToMaterial(mat);
            swissFaults.ApplyToMaterial(mat);
            perlinDune.ApplyToMaterial(mat);
            swissDune.ApplyToMaterial(mat);
            mntVar.ApplyToMaterial(mat);
            colorVar.ApplyToMaterial(mat);
            mountainBase.ApplyToMaterial(mat);
            hillBase.ApplyToMaterial(mat);
        }
    }
}
