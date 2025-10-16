using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour
{
    public Item item;
    [SerializeField] private Image Slot_Item_Image;
    private bool isIn = false;

    private void Awake()
    { 
        canvas = FindObjectOfType<Canvas>();
        
    }
    public void SetSlot(Item setitem, bool is_New_Item)
    {
        if(setitem != null)
        {
            get_Item(setitem);
            
            Slot_Update(setitem);
        }
    }
    private void get_Item(Item get_item)
    {
        get_item.set_isGet(true);

    }
    private void Slot_Update(Item update_Item)
    {
        SetColor(1f, Slot_Item_Image);
        this.Slot_Item_Image.sprite = update_Item.getSprite();
        this.isIn = true;
        this.item = update_Item;
    }
    private void Clear_Slot()
    {
        SetColor(0f, Slot_Item_Image);
        this.item = null;
        this.Slot_Item_Image = null;
        this.isIn=false;
    }
    private void SetColor(float alpha, Image setImage)
    {
        Color color = setImage.color;
        color.a = alpha;
        setImage.color = color;
    }
    private Canvas canvas;
    public void SetSlotItem(Item setitem) { item = setitem; }
    public void setSlotImage(Image SlotImage) { Slot_Item_Image = SlotImage; }
    public void Set_isIn(bool IsItem) { isIn = IsItem; }
    public bool isin() {  return isIn; } 
    public Item getItem() { return item; }
}
