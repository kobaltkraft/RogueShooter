using UnityEngine;

namespace RogueArena.Environment
{
    public static class PrimitiveBuilder
    {
        public static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().material = material;
            return instance;
        }
    }
}
