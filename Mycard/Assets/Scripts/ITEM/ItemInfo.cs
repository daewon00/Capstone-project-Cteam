using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfo : MonoBehaviour
{
    public RelicData RelicSO;

    public static ItemInfo instance;

    public Image relicSprite;

    //public Relic RelictoSpawn;
    //cardÂüÁ¶
    void Awake()
    {
        instance = this;
    }


    // Start is called before the first frame update
    void Start()
    {
        setupRelic();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void setupRelic()
    {
        //relicSprite.sprite = RelicSO.relicSprite;
    }
    public void AddRelic()
    {
        setupRelic();
        
        
        

    }
}
