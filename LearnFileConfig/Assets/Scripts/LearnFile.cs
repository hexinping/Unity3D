﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LearnFile : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var str = Utils.ReadConfigFile("text.txt");
        Debug.Log(str);
    }

}