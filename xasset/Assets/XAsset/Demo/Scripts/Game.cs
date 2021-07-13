﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using libx;
using UnityEngine;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
	public Dropdown dropdown;
	public Image temp;
    public Slider hotUpdateTestSlider;
	private string[] _assets;
	private int _optionIndex;

	List<GameObject> _gos = new List<GameObject> ();
	List<AssetRequest> _requests = new List<AssetRequest> ();

    public RawImage urlRawImage;
    public RawImage localRawImage;

	public void OnLoad ()
	{
		StartCoroutine (LoadAsset ());
	}

	AssetRequest LoadSprite (string path)
	{
		var request = Assets.LoadAsset (path, typeof(Sprite));
		_requests.Add (request);
		return request;
	}

    AssetRequest LoadSpriteAsync(string path)
    {
        var request = Assets.LoadAssetAsync(path, typeof(Sprite));
        _requests.Add(request);
        return request;
    }

    AssetRequest LoadGameObjectAsync(string path)
	{
		var request = Assets.LoadAssetAsync(path, typeof(GameObject));
		_requests.Add(request);
		return request;
	}


    AssetRequest LoadSceneAsync(string path)
    {
        var request = Assets.LoadSceneAsync(path, false);
        _requests.Add(request);
        return request;
    }

    AssetRequest LoadGameObject(string path)
	{
		var request = Assets.LoadAsset(path, typeof(GameObject));
		_requests.Add(request);
		return request;
	}

	public void OnLoadAll ()
	{ 
		StartCoroutine (LoadAll (_assets.Length));
	}

    public void OnLoadAllAsync()
    {
        StartCoroutine(LoadAllAsync(_assets.Length));
    }

    public void OnLoadScenelAsync()
    {
        string path = _assets[_optionIndex];
        if (string.IsNullOrEmpty(path)) return;
        var ext = Path.GetExtension(path);
        if (ext.Equals(".unity", StringComparison.OrdinalIgnoreCase))
                StartCoroutine(LoadSceneAsync(path));
    }

    IEnumerator LoadAll (int size)
	{
		var count = 0; 
		List<AssetRequest> list = new List<AssetRequest> ();
		for (int i = _optionIndex; i < _assets.Length; i++) {
			var asset = _assets [i];
			var ext = Path.GetExtension (asset);
			if (count >= size) {
				_optionIndex = i; 
				break;
			}
			if (ext.Equals (".png", StringComparison.OrdinalIgnoreCase)) {
				var request = LoadSprite (asset);
				request.completed += OnCompleted;  
				list.Add (request); 
				count++;
			}
		}
		yield return new WaitUntil (() => list.TrueForAll (o => {
			return o.isDone;
		}));
	}

    private bool _isLoadAllAsync = false;
    IEnumerator LoadAllAsync(int size)
    {
        _isLoadAllAsync = true;
        var count = 0;
        List<AssetRequest> list = new List<AssetRequest>();
        for (int i = _optionIndex; i < _assets.Length; i++)
        {
            var asset = _assets[i];
            var ext = Path.GetExtension(asset);
            if (count >= size)
            {
                _optionIndex = i;
                break;
            }
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                var request = LoadSpriteAsync(asset);
                request.completed += OnCompleted;
                list.Add(request);
                count++;
            }
        }
        yield return new WaitUntil(() => list.TrueForAll(o =>
        {
            return o.isDone;
        }));
    }

    private void OnCompleted (AssetRequest request)
	{
		if (!string.IsNullOrEmpty (request.error)) {
			request.Release ();
			return;
		}
		var go = Instantiate (temp.gameObject, temp.transform.parent);
		go.SetActive (true);
		go.name = request.asset.name;
		var image = go.GetComponent<Image> ();
		image.sprite = request.asset as Sprite;
		_gos.Add (go);
	}

	private IEnumerator LoadAsset ()
	{
		if (_assets == null || _assets.Length == 0) {
			yield break;
		}
        //根据当前下拉框选择的值去选取AB路径
        var path = _assets [_optionIndex];
        //获取拓展名
        var ext = Path.GetExtension (path);
		if (ext.Equals (".png", StringComparison.OrdinalIgnoreCase)) {
            //拿着这个路径去加载精灵图片
            var request = LoadSprite (path);
			yield return request;
			if (!string.IsNullOrEmpty (request.error)) {
				request.Release ();
				yield break;
			}
            //实例化
            var go = Instantiate (temp.gameObject, temp.transform.parent);
			go.SetActive (true);
			go.name = request.asset.name;
			var image = go.GetComponent<Image> ();
            //设置从AB加载出来的精灵图片
            image.sprite = request.asset as Sprite; 
			_gos.Add (go);
		}
	}

	public void OnUnload ()
	{
		_optionIndex = 0;
		StartCoroutine (UnloadAssets ());
	}

	private IEnumerator UnloadAssets ()
	{
		foreach (var image in _gos) {
			DestroyImmediate (image);
		}
		_gos.Clear ();
        
		foreach (var request in _requests) {
            //减少引用计数
            request.Release ();
		}

		_requests.Clear ();
		yield return null; 
	}

    private string hotUpatePrefabPath;
    private string hotUpdateScenePath;
	// Use this for initialization
	void Start ()
	{
		dropdown.ClearOptions ();
		_assets = Assets.GetAllAssetPaths ();
		foreach (var item in _assets) {

            Debug.Log($"#### {item}");
			var ext = Path.GetExtension(item);
			if (ext.Equals(".prefab", StringComparison.OrdinalIgnoreCase))
			{
				hotUpatePrefabPath = item;
			}
			dropdown.options.Add (new Dropdown.OptionData (item));
		}

		dropdown.onValueChanged.AddListener (OnDropdown);


        Test();

    }
    #region 测试下一些常用接口

    private void Test()
    {
        if (!string.IsNullOrEmpty(hotUpatePrefabPath))
        {
            Debug.Log($"hotUpatePrefabPath == {hotUpatePrefabPath}");
            //同步加载
            var abRequest = LoadGameObject(hotUpatePrefabPath);
            _requests.Add(abRequest);
            var goSync = Instantiate(abRequest.asset) as GameObject;
            goSync.SetActive(true);
            goSync.name = "HotTestSync";


            //异步加载
            var abRequestAsync = LoadGameObjectAsync(hotUpatePrefabPath);
            _requests.Add(abRequestAsync);
            abRequestAsync.completed += (AssetRequest request) =>
            {
                if (!string.IsNullOrEmpty(request.error))
                {
                    request.Release();
                    return;
                }
                var go = Instantiate(request.asset) as GameObject;
                go.SetActive(true);
                go.name = "HotTestAsync";
                go.transform.position = new Vector3(2, 0, 0);

            };

            //加载进度
            /*
                从Bundle中加载始终返回的是BundleAssetRequest或者BundleAssetRequestAsync
                1 同步加载  AssetRequest上progress始终为1 （BundleAssetRequest）
                2 异步加载  AssetRequest上progress ==》 BundleAssetRequestAsync
            */

            // animationClip 嵌入到gameObject上 OK
            // altas 测下 ==》OK  图集不打AB，对应的散图打成一个AB

            //打了一个资源模型 含有fbx texture material mesh 都通过了


            //场景加载 OK
            //var scene = Assets.LoadSceneAsync(gameScene, false);

            //网络加载 内部使用的是UnityWebRequest封装的
            /*
               if (path.StartsWith("http://", StringComparison.Ordinal) ||
                    path.StartsWith("https://", StringComparison.Ordinal) ||
                    path.StartsWith("file://", StringComparison.Ordinal) ||
                    path.StartsWith("ftp://", StringComparison.Ordinal) ||
                    path.StartsWith("jar:file://", StringComparison.Ordinal))
             */

            //网络资源  测试OK
            string url = "https://ss0.baidu.com/94o3dSag_xI4khGko9WTAnF6hhy/zhidao/pic/item/8326cffc1e178a82b13fb3d1f703738da977e844.jpg";
            var trequest = Assets.LoadAssetAsync(url, typeof(Texture2D));
            trequest.completed += (AssetRequest request) =>
            {
                var tex = request.asset as Texture2D;
                urlRawImage.texture = tex;
            };


            //本地资源 ==>TODO 后面写一个跨平台的接口获取本地路径
            string path = "file://" + Application.dataPath + "/hotUpdateTemp.jpg";
            var filerequest = Assets.LoadAssetAsync(path, typeof(Texture2D));
            filerequest.completed += (AssetRequest request) =>
            {
                var tex = request.asset as Texture2D;
                localRawImage.texture = tex;
            };

            path = "file://" + Application.dataPath + "/Title.txt";
            var textrequest = Assets.LoadAssetAsync(path, typeof(TextAsset));
            textrequest.completed += (AssetRequest request) =>
            {
                string str = request.text;
                Debug.Log($"Title.txt 内容是=={str}");
            };


            //UnityWebRequest request = UnityWebRequest.Get(@"http://localhost/fish.lua.txt");
            //yield return request.SendWebRequest();
            //string str = request.downloadHandler.text;
            //File.WriteAllText(@"D:\PlayerGamePackage\fish.lua.txt", str);


        }
    }

    private void Update()
    {
        if (!_isLoadAllAsync) return;
        if (_isLoadAllAsync)
        {
            int count = _requests.Count;
            if (count == 0)
            {
                hotUpdateTestSlider.value = 0;
                return;
            }
            //根据数量来标识进度  ==> 用数量来表现感觉更好
            int doneCount = 0;
            for (int i = 0; i < count; i++)
            {
                var request = _requests[i];
                if (request.isDone)
                    doneCount++;
            }
            float pro = (float)doneCount / count;
            Debug.Log($"curProgress == {pro} {doneCount} {count}");
            hotUpdateTestSlider.value = pro;

            //根据request的progr来标识进度

            //float rPro = 1.0f / count;
            //float curProgress = 0f;
            //for (int i = 0; i < count; i++)
            //{
            //    var request = _requests[i];
            //    //if (request.isDone)
            //    //    doneCount++;
            //    curProgress += request.progress * rPro;
            //}
            //Debug.Log($"curProgress == {curProgress}");
            //hotUpdateTestSlider.value = curProgress;
        }
    }
    #endregion

    private void OnDropdown (int index)
	{
		_optionIndex = index;
	}
}