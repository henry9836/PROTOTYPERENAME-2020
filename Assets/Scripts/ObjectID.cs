﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectID : MonoBehaviour
{
    public enum PlayerID
    {
        UNASSIGNED,
        PLAYER,
        AI_1,
        AI_2,
        AI_3,
        AI_4,
        AI_5,
        AI_6,
        AI_7,
        AI_8,
    }

    public enum OBJECTID
    {
        UNASSIGNED,
        UNIT,
        BUILDING,
        RESOURCE
    }

    public PlayerID ownerPlayerID = PlayerID.UNASSIGNED;
    public OBJECTID objID = OBJECTID.UNASSIGNED;
    //public int 

    public float health = 100.0f;
    public float maxHealth = 100.0f;
    public float velo = 0.0f;


    void Update()
    {
        if (objID == OBJECTID.UNIT) {
            this.gameObject.transform.GetChild(0).GetComponentInChildren<Image>().fillAmount = health / maxHealth;
        }

        if (health <= 0)
        {
            if (objID == OBJECTID.UNIT)
            {
                gameObject.GetComponent<sparks>().particleKill();
            }
            Destroy(gameObject);
        }
    }

}
