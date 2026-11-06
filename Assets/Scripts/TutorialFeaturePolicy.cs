public enum TutorialFeature { Skill, Adverb, Adjective, Muessen, Future }

// Pure availability rules. The caller supplies the running guide; nothing is saved or unlocked.
public static class TutorialFeaturePolicy
{
    public static bool Available(TutorialController guide, TutorialFeature feature) =>
        Available(guide != null && guide.RestrictsFeatures, guide == null ? 0 : guide.StageNumber, feature);

    public static bool Available(bool guiding, int stageNumber, TutorialFeature feature)
    {
        int introducedAt;
        switch (feature)
        {
            case TutorialFeature.Skill: introducedAt = 1; break;
            case TutorialFeature.Adverb: introducedAt = 2; break;
            case TutorialFeature.Adjective: introducedAt = 3; break;
            case TutorialFeature.Muessen: introducedAt = 4; break;
            case TutorialFeature.Future: introducedAt = 5; break;
            default: return false;
        }
        return !guiding || stageNumber < 1 || stageNumber > 5 || stageNumber >= introducedAt;
    }
}
