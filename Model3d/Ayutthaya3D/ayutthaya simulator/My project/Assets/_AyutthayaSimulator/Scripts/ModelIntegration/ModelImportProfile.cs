using AyutthayaSimulator.Core;
using UnityEngine;

namespace AyutthayaSimulator
{
    [CreateAssetMenu(fileName = "ModelImportProfile", menuName = "Ayutthaya Simulator/Model Import Profile")]
    public sealed class ModelImportProfile : ScriptableObject
    {
        public ModelKind Kind = ModelKind.Unknown;
        public bool ManualOverride;
        public string Notes = "";
    }
}
