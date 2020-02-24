﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIBehaviour : MonoBehaviour
{

    public class scoutedObject
    {
        public scoutedObject(GameObject _obj)
        {
            obj = _obj;
            positionSpotted = obj.transform.position;
            objID = obj.GetComponent<ObjectID>();
            ownerID = objID.ownerPlayerID;
            objType = objID.objID;

            if (obj.GetComponent<TCController>())
            {
                isTC = true;
            }

        }

        public GameObject obj;
        public ObjectID objID;
        public bool isTC = false;
        public float distanceFromUs;
        public Vector3 positionSpotted;
        public ObjectID.OBJECTID objType;
        public ObjectID.PlayerID ownerID;
    };

    public List<scoutedObject> seenObjects = new List<scoutedObject>();
    public int playerID = -1;
    public Vector2 profitCheckRandomRange = new Vector2(5.0f, 20.0f);

    public List<float> balanceHistory = new List<float>();
    private List<GameObject> units = new List<GameObject>();
    private List<GameObject> idleUnits = new List<GameObject>();
    private GameManager GM;
    private ObjectID objID;
    public float profitCheckTimer = 0.0f;
    public float profitCheckThreshold = 7.0f;
    public float lastBalanceAvg = Mathf.Infinity;
    private float knowledgeTimer = 0.0f;
    private float knowledgeThreshold = 5.0f;
    private scoutedObject closestKnownResource = null;
    private scoutedObject closestKnownEnemyUnit = null;
    private scoutedObject closestKnownEnemyBuilding = null;
    private scoutedObject closestKnownEnemyTC = null;
    private TCController TC;
    private GameObject scoutUnit;

    public void regNewSeenObject(GameObject obj)
    {
        //Filter out seen objects and add to list
        StartCoroutine(regNewSeenObjectCoroutine(obj));
    }

    void cullNulls()
    {
        StartCoroutine(cullNullsCoroutine());
    }

    void profitCheck()
    {
        profitCheckTimer += Time.unscaledDeltaTime;

        //Debug.Log($"{profitCheckTimer}/{profitCheckThreshold}");

        if (profitCheckTimer >= profitCheckThreshold)
        {

            //Reset timer
            profitCheckThreshold = Random.Range(profitCheckRandomRange.x, profitCheckRandomRange.y);
            profitCheckTimer = 0.0f;

            //Do we have enough history to get avg?
            if (balanceHistory.Count >= 5)
            {
                float avg = 0.0f;
                
                //Get average
                for (int i = 0; i < balanceHistory.Count; i++)
                {
                    avg += balanceHistory[i];
                }
                avg /= balanceHistory.Count;

                Debug.Log($"Have a avg of ${avg} over the ${lastBalanceAvg} of last time");

                //Is the avg not higher than our last average?
                if (avg < lastBalanceAvg)
                {
                    //Do we know any resources
                    if (closestKnownResource != null)
                    {

                    }
                    else
                    {
                        //Scout
                        Scout();
                    }

                    Debug.Log($"Spawning Unit...");
                    //Make the profit things happen
                    

                    //Spawn unit
                    units.Add(TC.SpawnUnit());

                    

                }

                //Update last balance and clear list
                lastBalanceAvg = avg;
                balanceHistory.Clear();

            }
            //Add onto history
            else
            {
                balanceHistory.Add(GM.GetResouceCount(playerID));
            }

        }
    }

    void CreateScout()
    {

    }

    void Scout()
    {
        if (scoutUnit == null)
        {
            CreateScout();
        }
    }

    void attackLogic()
    {

    }

    void updateKnowledge()
    {
        knowledgeTimer += Time.unscaledDeltaTime;
        if (knowledgeTimer >= knowledgeThreshold)
        {
            knowledgeTimer = 0.0f;
            StartCoroutine(updateKnowledgeCoroutine());
        }

    }

    // Start is called before the first frame update
    void Start()
    {
        objID = GetComponent<ObjectID>();
        if (objID.ownerPlayerID == ObjectID.PlayerID.UNASSIGNED)
        { 
            playerID = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>().RequestID((int)ObjectID.PlayerID.AI_1);
            objID.ownerPlayerID = (ObjectID.PlayerID)playerID;
        }
        else
        {
            playerID = (int)objID.ownerPlayerID;
        }

        //Set up timers
        profitCheckThreshold = Random.Range(profitCheckRandomRange.x, profitCheckRandomRange.y);

        //Find References
        GM = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();
        TC = GetComponent<TCController>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        cullNulls();
        updateKnowledge();
        profitCheck();
        attackLogic();
    }

    IEnumerator regNewSeenObjectCoroutine(GameObject obj)
    {
        bool seen = false;
        //Have we seen this object?
        for (int i = 0; i < seenObjects.Count; i++)
        {
            if (seenObjects[i].obj == obj)
            {
                seen = true;
                //Update position
                seenObjects[i].positionSpotted = obj.transform.position;
                break;
            }

            yield return null;
        }

        //Add to list
        if (!seen)
        {
            seenObjects.Add(new scoutedObject(obj));
        }

        yield return null;
    }

    IEnumerator cullNullsCoroutine()
    {
        //For all seen objs
        for (int i = 0; i < seenObjects.Count; i++)
        {
            //Is the obj null?
            if (seenObjects[i].obj == null)
            {
                //Remove obj if null
                seenObjects.RemoveAt(i);
            }
            yield return null;
        }

        for (int i = 0; i < units.Count; i++)
        {
            //Is the obj null?
            if (units[i] == null)
            {
                //Remove obj if null
                units.RemoveAt(i);
            }
            yield return null;
        }

        yield return null;
    }

    IEnumerator updateKnowledgeCoroutine()
    {
        //Reset knowledge
        closestKnownEnemyBuilding = null;
        closestKnownEnemyTC = null;
        closestKnownEnemyUnit = null;
        closestKnownResource = null;

        float closestKnownEnemyBuildingDistance = Mathf.Infinity;
        float closestKnownEnemyTCDistance = Mathf.Infinity;
        float closestKnownEnemyUnitDistance = Mathf.Infinity;
        float closestKnownResourceDistance = Mathf.Infinity;

        for (int i = 0; i < seenObjects.Count; i++)
        {
            //null check
            if (seenObjects[i].obj != null)
            {
                if (seenObjects[i].objType == ObjectID.OBJECTID.BUILDING)
                {
                    if (seenObjects[i].isTC)
                    {
                        if (Vector3.Distance(transform.position, seenObjects[i].positionSpotted) < closestKnownEnemyTCDistance)
                        {
                            closestKnownEnemyTC = seenObjects[i];
                            closestKnownEnemyTCDistance = Vector3.Distance(transform.position, seenObjects[i].positionSpotted);
                        }
                    }
                    else {
                        if (Vector3.Distance(transform.position, seenObjects[i].positionSpotted) < closestKnownEnemyBuildingDistance)
                        {
                            closestKnownEnemyBuilding = seenObjects[i];
                            closestKnownEnemyBuildingDistance = Vector3.Distance(transform.position, seenObjects[i].positionSpotted);
                        }
                    }
                }
                else if (seenObjects[i].objType == ObjectID.OBJECTID.UNIT)
                {
                    if (Vector3.Distance(transform.position, seenObjects[i].positionSpotted) < closestKnownEnemyUnitDistance)
                    {
                        closestKnownEnemyUnit = seenObjects[i];
                        closestKnownEnemyUnitDistance = Vector3.Distance(transform.position, seenObjects[i].positionSpotted);
                    }
                }
                else if (seenObjects[i].objType == ObjectID.OBJECTID.RESOURCE)
                {
                    if (Vector3.Distance(transform.position, seenObjects[i].positionSpotted) < closestKnownResourceDistance)
                    {
                        closestKnownResource = seenObjects[i];
                        closestKnownResourceDistance = Vector3.Distance(transform.position, seenObjects[i].positionSpotted);
                    }
                }
            }
        }

        //Check for idle workers
        idleUnits.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].GetComponent<AIDroneController>().isIdle())
            {
                idleUnits.Add(units[i]);
            }
        }


        yield return null;
    }

}
