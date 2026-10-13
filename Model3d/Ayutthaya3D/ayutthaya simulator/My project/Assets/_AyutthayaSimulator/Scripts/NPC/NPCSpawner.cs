using System.Collections.Generic;
using UnityEngine;

namespace AyutthayaSimulator
{
    public sealed class NPCSpawner : MonoBehaviour
    {
        readonly List<NPCController> pool = new List<NPCController>();

        public NPCController Spawn(NPCController prefab, Vector3 position)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].gameObject.activeSelf)
                {
                    pool[i].transform.position = position;
                    pool[i].gameObject.SetActive(true);
                    return pool[i];
                }
            }
            if (prefab == null) return null;
            var created = Instantiate(prefab, position, Quaternion.identity, transform);
            pool.Add(created);
            return created;
        }

        public void Release(NPCController npc)
        {
            if (npc != null) npc.gameObject.SetActive(false);
        }
    }
}
