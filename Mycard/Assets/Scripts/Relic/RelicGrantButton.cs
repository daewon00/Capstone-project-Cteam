using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelicGrantButton : MonoBehaviour
{
    public void AddRelicTest()
    {
        RelicSystem.Instance.AddRelicById("ExtraDraw", stacks: 1);
    }
}
