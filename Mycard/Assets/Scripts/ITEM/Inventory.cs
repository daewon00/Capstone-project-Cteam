using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Inventory : MonoBehaviour
{
    public Slot[] itemSlots;
    public static Inventory Instance;

    private void Awake()
    {
        Instance = this;  // 싱글톤 초기화
    }

    public void AddItem(Item item)
    {
        if(itemSlots != null)
        {
            for (int i = 0; i < itemSlots.Length; i++)
            {
                if (!itemSlots[i].isin())
                {
                    Item newItem = Instantiate(item);

                    itemSlots[i].SetSlot(newItem, true);

                    Debug.Log("newItem In");
                    return;
                }
            }
        }
    }
}
