using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

public class Item : MonoBehaviour
{
    protected int ItemNumber;
    protected string ItemName;
    protected Image ItemImage;
    protected Sprite ItemSprite;
    protected int get_Count = 0;
    protected bool isget = false;
    
    public int getItemNumber()
    {
        return this.getItemNumber();
    }

    public string getItemName()
    {
        return this.getItemName();
    }

    public int get_Item_Count()
    {
        return this.get_Count;
    }
    public void set_Item_Count(int get_count)
    {
        this.get_Count = get_count;
    }

    public void add_Item_Count(int add_count)
    {
        this.get_Count += add_count;
    }

    public bool isSameItem(Item otherItem)
    {
        return this.ItemNumber == otherItem.getItemNumber();
    }

    public void set_isGet(bool isget)
    {
        this.isget = isget;
    }

    public bool get_isGet()
    {
        return this.isget;
    }
    public Sprite getSprite() { { return this.ItemSprite; } }
    public Image getImage() { return this.ItemImage; }

    public void setSprite(Sprite itemSprite)
    {
        this.ItemSprite = itemSprite;
    }
    //추가되면 작동
    public virtual void OnAddItem() { }

    public virtual void OnPlayerCardPlaced(Card card) { }

}
//이코드를 상속해서 아이템 만들기