﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIEnterAnimation:UIAnimationHxp
{
    public bool isEnter = true;
    private void Awake()
    {
        isAutoPlay = false;
    }
    private void Start()
    {
        //重载父类的 必须保留
    }
}


