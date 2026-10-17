using System.Collections.Generic;
using UnityEngine;

namespace TappyTale.BossFight.Data
{
    [CreateAssetMenu(menuName = "Tappy Tale/Boss Fight/Boss Database", fileName = "BossDatabase")]
    public sealed class BossDatabase : ScriptableObject
    {
        [SerializeField] private List<BossDefinition> bosses = new();

        public IReadOnlyList<BossDefinition> Bosses => bosses;

        public bool TryGet(string id, out BossDefinition definition)
        {
            for (int i = 0; i < bosses.Count; i++)
            {
                BossDefinition candidate = bosses[i];
                if (candidate != null && candidate.Id == id)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
