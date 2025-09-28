using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelicGrantButton : MonoBehaviour
{
    public GameObject RelicButton;

    public void AddRelicTest1()
    {
        RelicSystem.Instance.AddRelicById("extradraw");


    }
    public void AddRelicTest2()
    {

        RelicSystem.Instance.AddRelicById("HPup");
    }
    public void AddRelicTest3()
    {

        RelicSystem.Instance.AddRelicById("MANAup");


    }
    public void AddRelicTest4()
    {

        RelicSystem.Instance.AddRelicById("cardhp");


    }
    public void AddRelicTest5()
    {

        RelicSystem.Instance.AddRelicById("manadis");

    }

    public void AddRelicTest6()
    {

        RelicSystem.Instance.AddRelicById("cardattackup");

    }
    public void AddRelicTest7()
    {

        RelicSystem.Instance.AddRelicById("finalattack");

    }

    public void AddRelicTest8()
    {

        RelicSystem.Instance.AddRelicById("Gold");

    }

    public void AddRelicTest9()
    {

        RelicSystem.Instance.AddRelicById("drawManaDiscount");

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
