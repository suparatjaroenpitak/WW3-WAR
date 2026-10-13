using System;

namespace AyutthayaSimulator.Core
{
    public enum ModelKind
    {
        Unknown,
        Building,
        Vehicle,
        Npc,
        Door,
        Shop,
        Prop,
        Environment
    }

    public enum BindingDecision
    {
        Reject,
        CandidateNeedsConfirmation,
        Bind
    }

    public sealed class ModelNodeFacts
    {
        public string Name = "";
        public bool HasMesh;
        public bool HasRendererBounds;
        public bool ManualOverride;
        public ModelKind OverrideKind;
        public int ChildCount;
        public bool ScaleIsRoughlyMetric = true;
    }

    public readonly struct BindingResult
    {
        public readonly BindingDecision Decision;
        public readonly ModelKind Kind;
        public readonly string Reason;

        public BindingResult(BindingDecision decision, ModelKind kind, string reason)
        {
            Decision = decision;
            Kind = kind;
            Reason = reason;
        }
    }

    /// <summary>
    /// Decides whether a hierarchy node may receive a gameplay component.
    /// A name token alone is never enough.
    /// </summary>
    public static class ModelBindingRules
    {
        public static BindingResult Classify(ModelNodeFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (facts.ManualOverride)
            {
                if (!facts.HasMesh && facts.OverrideKind != ModelKind.Building && facts.OverrideKind != ModelKind.Environment)
                    return new BindingResult(BindingDecision.Reject, facts.OverrideKind, "Manual kind requires a mesh except for an empty building/environment root.");
                return new BindingResult(BindingDecision.Bind, facts.OverrideKind, "Manual override accepted after mesh check.");
            }

            var token = TokenKind(facts.Name);
            if (token == ModelKind.Unknown)
                return new BindingResult(BindingDecision.Reject, ModelKind.Unknown, "No manual override and no recognized name token.");
            if (!facts.HasMesh)
                return new BindingResult(BindingDecision.Reject, token, "Name token '" + facts.Name + "' has no mesh. Name alone is not evidence.");
            if (!facts.HasRendererBounds)
                return new BindingResult(BindingDecision.Reject, token, "Mesh has no renderer bounds.");
            if (!facts.ScaleIsRoughlyMetric)
                return new BindingResult(BindingDecision.CandidateNeedsConfirmation, token, "Scale is far from 1 unit = 1 metre. Confirm before binding.");
            return new BindingResult(BindingDecision.CandidateNeedsConfirmation, token, "Name token plus mesh is a candidate. Confirm in the Inspector before treating it as verified.");
        }

        public static ModelKind TokenKind(string name)
        {
            if (string.IsNullOrEmpty(name)) return ModelKind.Unknown;
            var lower = name.ToLowerInvariant();
            if (Contains(lower, "door")) return ModelKind.Door;
            if (Contains(lower, "wheel")) return ModelKind.Vehicle;
            if (Contains(lower, "vehicle") || Contains(lower, "car")) return ModelKind.Vehicle;
            if (Contains(lower, "npc") || Contains(lower, "character")) return ModelKind.Npc;
            if (Contains(lower, "shop")) return ModelKind.Shop;
            if (Contains(lower, "building") || Contains(lower, "house")) return ModelKind.Building;
            if (Contains(lower, "road") || Contains(lower, "terrain") || Contains(lower, "tree")) return ModelKind.Environment;
            if (Contains(lower, "prop")) return ModelKind.Prop;
            return ModelKind.Unknown;
        }

        static bool Contains(string value, string token) => value.IndexOf(token, StringComparison.Ordinal) >= 0;
    }
}
