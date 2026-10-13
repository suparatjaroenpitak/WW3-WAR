using System;
using System.Collections.Generic;

namespace AyutthayaSimulator
{
    [Serializable]
    public sealed class AssetValidationReport
    {
        public string AssetName = "";
        public string GeneratedAtUtc = "";
        public bool Passed;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public int MeshCount;
        public int MissingMaterialCount;
        public int RendererCount;
    }
}
