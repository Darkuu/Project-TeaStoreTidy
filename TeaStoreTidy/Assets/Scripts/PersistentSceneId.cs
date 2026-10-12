using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Creates stable IDs from the scene hierarchy as it exists when play starts.</summary>
public static class PersistentSceneId
{
    public static string FromTransform(Transform target)
    {
        if (target == null)
            return string.Empty;

        var hierarchy = new Stack<Transform>();
        for (Transform current = target; current != null; current = current.parent)
            hierarchy.Push(current);

        var builder = new StringBuilder(target.gameObject.scene.path);
        if (builder.Length == 0)
            builder.Append(target.gameObject.scene.name);

        while (hierarchy.Count > 0)
        {
            Transform current = hierarchy.Pop();
            builder.Append('/').Append(current.GetSiblingIndex()).Append(':').Append(current.name);
        }

        return builder.ToString();
    }
}
