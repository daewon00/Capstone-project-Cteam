using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelicGrantButton : MonoBehaviour
{
    public GameObject RelicButton;

    public void AddRelicTest1()
    {
        RelicSystem.Instance.AddRelicById("WarBanner", stacks: 1);


    }
    public void AddRelicTest2()
    {

        RelicSystem.Instance.AddRelicById("ManaGem", stacks: 1);

    }
    public void AddRelicTest3()
    {

        RelicSystem.Instance.AddRelicById("HappyFlower", stacks: 1);


    }
    public void AddRelicTest4()
    {

        RelicSystem.Instance.AddRelicById("ExtraDraw", stacks: 1);


    }
    public void AddRelicTest5()
    {

        RelicSystem.Instance.AddRelicById("SheildBanner", stacks: 1);

    }
    public void AddRelicTest6()
    {

        RelicSystem.Instance.AddRelicById("ManaDiscount", stacks: 1);


    }
    public void AddRelicTest7()
    {

        RelicSystem.Instance.AddRelicById("EnemyManaLeech", stacks: 1);


    }
    public void AddRelicTest8()
    {

        RelicSystem.Instance.AddRelicById("EnemyFirstCardWeakener", stacks: 1);


    }
    public void AddRelicTest9()
    {

        RelicSystem.Instance.AddRelicById("COMP_COMP_Knight", stacks: 1);

    }

    public void RelicButtonOn()
    {
        if (RelicButton.activeSelf == false)
        {
            RelicButton.SetActive(true);

        }
        else
        {
            RelicButton.SetActive(false);
        }
        
    }

}
