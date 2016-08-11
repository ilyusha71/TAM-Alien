/**********************************************************************
 * Graffiti Controller v1.0c
 * By iLYuSha Wakaka KocmocA
 * 
 * 08/05
 * 九種顏色，兩種筆刷，附加三種背景，自動存檔，自動播放，自定義設定
 * 
 * 08/08
 * Auto save: 移除自動存檔
 * Preload: 取消預載入詢問，增快讀取速度
 * Setting: 增加設定資訊保存本地端保存，啟動即載入設定
 * 
 * 08/10
 * Printbrush: 水彩刷淡效果
 **********************************************************************/
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Drawing;

public class GraffitiController : MonoBehaviour
{
    [System.Serializable]
    public struct Range
    {
        public int max;
        public int min;
        internal float value;
    }

    public GameObject opening;
    public Text textOrderLoading;
    private string folderPath = "D:/Alien Collections/";
    private bool checkLoad = false;

    public Transform renderCanvas;
    public enum RenderMode
    {
        Opening = 0,
        Show = 1,
        LineRender,
        SpriteRender,
    }
    public RenderMode renderMode;
    private RenderMode lastMode = RenderMode.LineRender;
    public GameObject[] lineBrush;
    public GameObject[] spriteBrush;

    /* Draw */
    public GameObject tool;
    private GameObject paintbrushChoosing;
    private UnityEngine.Color colorChoosing;
    private GameObject paintbrushClone;
    private int layer = 0;
    private bool drawing;

    /* Line Render Draw */
    private LineRenderer line;
    private int i;
    // Spirte Render Draw
    private SpriteRenderer sprite;
    private SpriteRenderer interpolationSprite;
    private Vector3 newDrawPos;
    private Vector3 lastDrawPos;
    private Vector3 posInterpolation;
    private float drawDistance;
    private int numInterpolation;
    private int k;
    public AnimationCurve aa;

    /* Screenshot & Save */
    public Camera myCamera;
    public Texture2D[] collections = new Texture2D[20];
    private float recordRatio;
    private float recordWidth;
    private int orderSave = 0;
    private bool saved = false;

    /* Auto Show Timer */
    public Range delayAutoShow = new Range();
    private float timerAutoShow;

    /* Show */
    public RawImage imgShow;
    public Text textOrder;
    public GameObject iconPlay;
    public GameObject iconPause;
    public Range durationShow = new Range();
    private float timerNextShow;
    private int orderShow = 0;
    private bool play;

    /* Setting */
    public GameObject panelSetting;
    public Scrollbar barDurationShow;
    public Text textDurationShow;
    public Scrollbar barAutoShow;
    public Text textAutoShow;

    void Awake()
    {
        recordRatio = 5.0f / 6.0f;
        recordWidth = myCamera.pixelWidth * recordRatio;
    }

    void Start()
    {
        InitialSetting();
        StartCoroutine(ReadCollections());
        paintbrushChoosing = lineBrush[0];
        colorChoosing = UnityEngine.Color.red;
    }

    void Update()
    {
        if (renderMode == RenderMode.LineRender || renderMode == RenderMode.SpriteRender)
        {
            if (Time.time > timerAutoShow)
                StartShow(true);
        }
        else if (renderMode == RenderMode.Show)
        {
            if (Input.GetKeyDown(KeyCode.F10))
                panelSetting.SetActive(!panelSetting.activeSelf);
            if (Time.time > timerNextShow && play)
                NextShow();
            if (!play)
            {
                if (Time.time > timerAutoShow)
                    PlayPause();
            }
        }

        if (Input.mousePosition.x < recordWidth)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Working();
                if (renderMode == RenderMode.LineRender)
                    StartLine();
                else if (renderMode == RenderMode.SpriteRender)
                    StartSprite(true);
            }
            else if (Input.GetMouseButtonUp(0))
                StopDraw();
            else if (Input.GetMouseButton(0))
            {
                Working();
                if (renderMode == RenderMode.LineRender)
                {
                    newDrawPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15));
                    if (newDrawPos != lastDrawPos && i < 200 && line != null && drawing)
                    {
                        i++;
                        line.SetVertexCount(i);
                        line.SetPosition(i - 1, newDrawPos);
                    }
                    else
                        StartLine();
                    lastDrawPos = newDrawPos;
                }
                else if (renderMode == RenderMode.SpriteRender)
                {
                    if (drawing)
                    {
                        /* Interpolation */
                        #region Interpolation
                        newDrawPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15));
                        drawDistance = Vector3.Distance(lastDrawPos, newDrawPos);
                        float interpolatation;
                        if (paintbrushChoosing == spriteBrush[0])
                            interpolatation = 0.3f;
                        else
                            interpolatation = 0.1f;
                        numInterpolation = (int)(drawDistance / interpolatation);
                        if (numInterpolation > 0)
                        {
                            for (int i = 0; i < numInterpolation; i++)
                            {
                                posInterpolation = lastDrawPos + (newDrawPos - lastDrawPos) * (i + 1) / (numInterpolation + 1);
                                DrawSprite(posInterpolation);
                            }
                        }
                        #endregion
                    }
                    StartSprite(!drawing);
                }
            }
        }
        else
            StopDraw();
    }

    #region Preload
    IEnumerator ReadCollections()
    {
        orderSave = PlayerPrefs.GetInt("LastOrder");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        yield return new WaitForSeconds(0.01f);

        for (int i = 0; i < collections.Length; i++)
        {
            WWW collection = new WWW("file:///" + folderPath + "Collections " + i + ".png");
            yield return collection;

            if (collection != null && string.IsNullOrEmpty(collection.error))
            {
                collections[i] = collection.texture;
                textOrderLoading.text = "作品 " + (i + 1) + " 載入成功";
                yield return new WaitForEndOfFrame();
            }
            else
            {
                if (!checkLoad)
                {
                    orderSave = i;
                    checkLoad = true;
                }
                textOrderLoading.text = "作品 " + (i + 1) + " 無資料";
                yield return new WaitForEndOfFrame();
            }
        }
        textOrderLoading.text = "作品集載入完成";
        opening.SetActive(false);
        StartDraw();
    }
    #endregion

    #region Draw
    public void StartDraw()
    {
        panelSetting.SetActive(false);
        Working();
        renderMode = lastMode;
        imgShow.gameObject.SetActive(false);
        tool.SetActive(true);
        saved = false;
    }
    void Working()
    {
        timerAutoShow = Time.time + delayAutoShow.value;
    }
    public void BurshChoosing(int orderBrush)
    {
        if (orderBrush < lineBrush.Length)
        {
            renderMode = RenderMode.LineRender;
            paintbrushChoosing = lineBrush[orderBrush];
        }
        else
        {
            renderMode = RenderMode.SpriteRender;
            paintbrushChoosing = spriteBrush[orderBrush - lineBrush.Length];
        }
        Working();
    }
    public void ColorChoosing(GameObject paintBucket)
    {
        colorChoosing = paintBucket.GetComponent<UnityEngine.UI.Image>().color;
        Working();
    }
    void StartLine()
    {
        drawing = true;
        paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);
        paintbrushClone.transform.SetParent(renderCanvas);
        line = paintbrushClone.GetComponent<LineRenderer>();
        // Pen
        if (paintbrushChoosing == lineBrush[0])
        {
            line.SetWidth(0.3f, 0.3f);
            colorChoosing.a = 1.0f;
        }
        // Watercolor
        else
        {
            line.SetWidth(0.7f, 0.7f);
            colorChoosing.a = 0.77f;
        }        
        line.SetColors(colorChoosing, colorChoosing);
        i = 0;
        // new line layer > old line layer
        layer++;
        line.sortingOrder = layer;
    }
    void StartSprite(bool first)
    {
        if (first)
            k = 0;
        drawing = true;
        newDrawPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15));
        DrawSprite(newDrawPos);
        layer++;
        lastDrawPos = newDrawPos;
    }
    void DrawSprite(Vector3 pos)
    {
        paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);
        paintbrushClone.transform.SetParent(renderCanvas);
        sprite = paintbrushClone.GetComponent<SpriteRenderer>();
        if (paintbrushChoosing == spriteBrush[0])
            colorChoosing.a = 1.0f;
        else
        {
            colorChoosing.a = aa.Evaluate(k*0.001f);
        }
            //colorChoosing.a = 0.37f  - k * 0.001f;
        sprite.color = colorChoosing;
        sprite.sortingOrder = layer;
        sprite.transform.position = pos;
        k++;
    }
    void StopDraw()
    {
        drawing = false;
    }
    public void Redraw()
    {
        Transform[] obj = renderCanvas.GetComponentsInChildren<Transform>();
        for (int i = 1; i < obj.Length; i++)
        {
            Destroy(obj[i].gameObject);
        }
       this.i = 0;
        layer = 0;
        newDrawPos = lastDrawPos = Vector3.zero;
        if (saved)
        {
            orderSave++;
            if (orderSave == collections.Length)
                orderSave = 0;
            saved = false;
            PlayerPrefs.SetInt("LastOrder", orderSave);
        }      
    }
    #endregion

    #region Save
    public void SavePic()
    {
        if(layer > 0)
            StartCoroutine(Screenshot());
    }
    IEnumerator Screenshot()
    {
        //在擷取畫面之前請等到所有的Camera都Render完
        yield return new WaitForEndOfFrame();

        Texture2D texture = new Texture2D((int)recordWidth, (int)myCamera.pixelHeight, TextureFormat.RGB24, false);
        //擷取作畫範圍 parm: recordWidth = 1600 (e.g. 1920 x 1080)
        texture.ReadPixels(new Rect(0, 0, (int)recordWidth, (int)myCamera.pixelHeight), 0, 0, false);
        texture.Apply();

        /* Save */
        SaveTextureToFile(texture, "Collections " + orderSave);
        collections[orderSave] = texture;
        Debug.LogWarning("Save to Collections [" + orderSave + "]");
        orderShow = orderSave;
        saved = true;

        StartShow(false);
    }
    void SaveTextureToFile(Texture2D texture, string fileName)
    {
        byte[] bytes = texture.EncodeToPNG();
        string filePath = folderPath + fileName + ".png";
        using (FileStream fs = File.Open(filePath, FileMode.Create))
        {
            BinaryWriter binary = new BinaryWriter(fs);
            binary.Write(bytes);
        }
    }
    #endregion

    #region Show
    public void StartShow(bool play)
    {
        for (int i = 0; i < collections.Length; i++)
        {
            if (collections[i] != null)
            {
                lastMode = renderMode;
                renderMode = RenderMode.Show;
                Redraw();
                imgShow.gameObject.SetActive(true);
                tool.SetActive(false);
                this.play = play;
                iconPause.SetActive(play);
                iconPlay.SetActive(!play);
                break;
            }
        }
        NextShow();
    }
    public void NextShow()
    {
        timerNextShow = Time.time + durationShow.value;
        while (collections[orderShow] == null)
        {
            orderShow++;
            if (orderShow == collections.Length)
                orderShow = 0;
        }
        imgShow.texture = collections[orderShow];
        textOrder.text = "(" + (orderShow+1) + ")";
        orderShow++;
        if (orderShow == collections.Length)
            orderShow = 0;
    }
    public void PlayPause()
    {
        play = !play;
        iconPause.SetActive(play);
        iconPlay.SetActive(!play);
        timerNextShow = Time.time + 1.0f;
    }
    public void PrevShow()
    {
        timerNextShow = Time.time + durationShow.value;
        orderShow -= 2;
        if (orderShow == -1)
            orderShow = collections.Length-1;
        else if (orderShow == -2)
            orderShow = collections.Length-2;
        while (collections[orderShow] == null)
        {
            orderShow--;
            if (orderShow == -1)
                orderShow = collections.Length-1;
        }
        //Debug.Log("Show: " + orderShow);
        imgShow.texture = collections[orderShow];
        textOrder.text = "(" + (orderShow + 1) + ")";
        orderShow++;
        if (orderShow == collections.Length)
            orderShow = 0;
    }
    #endregion

    #region Setting
    void InitialSetting()
    {
        barDurationShow.value = PlayerPrefs.GetFloat("DurationShow");
        SetDurationShowTime();
        barAutoShow.value = PlayerPrefs.GetFloat("AutoShow");
        SetAutoShowTime();
    }
    public void SetDurationShowTime()
    {
        durationShow.value = barDurationShow.value * (durationShow.max- durationShow.min) + durationShow.min;
        textDurationShow.text = "" + (int)durationShow.value;
        PlayerPrefs.SetFloat("DurationShow", barDurationShow.value);
    }
    public void SetAutoShowTime()
    {
        delayAutoShow.value = barAutoShow.value * (delayAutoShow.max - delayAutoShow.min) + delayAutoShow.min;
        textAutoShow.text = "" + (int)delayAutoShow.value;
        PlayerPrefs.SetFloat("AutoShow", barAutoShow.value);
    }
    #endregion
}