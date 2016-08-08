using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;
using System.Drawing;

public class GraffitiController : MonoBehaviour
{
    public GameObject opening;
    public Text textOrderLoading;
    private string folderPath = "E:/Alien Collections/";
    private bool checkLoad = false;

    public Transform renderCanvas;
    public enum RenderMode
    {
        Opening,
        LineRender,
        SpriteRender,
        Show,
    }
    public RenderMode renderMode;
    private RenderMode lastMode = RenderMode.LineRender;
    public GameObject[] paintbrush;

    /* Draw */
    public GameObject tool;
    private GameObject paintbrushChoosing;
    private UnityEngine.Color colorChoosing;
    private GameObject paintbrushClone;
    private int layer = 0;
    private bool firstPaint;
    /* Line Render Draw */
    private LineRenderer line;
    private int i;
    // Spirte Render Draw
    private SpriteRenderer sprite;
    private SpriteRenderer interpolationSprite;
    private Vector3 newDrawPos;
    private Vector3 lastDrawPos;
    private float drawDistance;
    private Vector3 posInterpolation;
    private int numInterpolation;

    /* Idle Timer */
    private float delayAutoSave = 5.0f;
    private float delayAutoShow = 20.0f;
    private float timerAutoSave = 0.0f;
    private float timerAutoShow = 0.0f;

    /* Screenshot & Save */
    public Camera myCamera;
    private Texture2D[] collections = new Texture2D[20];
    private float recordRatio;
    private float recordWidth;
    public int orderSave = 0;
    public bool saved = false;

    /* Show */
    public RawImage imgShow;
    public Text textOrder;
    public GameObject iconPlay;
    public GameObject iconPause;
    private float delayShow = 3.0f;
    private float timerShow = 0.0f;
    private int orderShow = 0;
    private bool play;

    /* Setting */
    public GameObject panelSetting;

    public Scrollbar intervalBar;
    public Text textIntervalShow;

    public Scrollbar autoSaveBar;
    public Text textAutoSave;

    public Scrollbar autoShowBar;
    public Text textAutoShow;

    void Awake()
    {
        recordRatio = 5.0f / 6.0f;
        recordWidth = myCamera.pixelWidth * recordRatio;
    }

    void Start()
    {
        StartCoroutine(ReadCollections());
        paintbrushChoosing = paintbrush[0];
        colorChoosing = UnityEngine.Color.red;
    }

    void Update()
    {
        //Debug.Log(layer);
        if (renderMode == RenderMode.LineRender || renderMode == RenderMode.SpriteRender)
        {
            if (Time.time > timerAutoSave)
                SavePic();
            if (Time.time > timerAutoShow)
                StartShow();
        }
        else if (renderMode == RenderMode.Show)
        {
            if (Input.GetKeyDown(KeyCode.F10))
                panelSetting.SetActive(!panelSetting.activeSelf);
            if (Time.time > timerShow && play)
                NextShow();
        }


        if (Input.GetKeyDown(KeyCode.K))
            StartCoroutine(Screenshot());
        if (Input.GetKeyDown(KeyCode.V))
            StartShow();

        if (Input.mousePosition.x < recordWidth)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Working();
                if (renderMode == RenderMode.LineRender)
                {
                    paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);
                    paintbrushClone.transform.SetParent(renderCanvas);
                    line = paintbrushClone.GetComponent<LineRenderer>();
                    line.SetColors(colorChoosing, colorChoosing);
                    line.SetWidth(0.3f, 0.3f);
                    i = 0;
                    // new line layer > old line layer
                    layer++;
                    line.sortingOrder = layer;
                }
                else if (renderMode == RenderMode.SpriteRender)
                {
                    newDrawPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15));
                    paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);//克隆一个带有LineRender的物体  
                    paintbrushClone.transform.SetParent(renderCanvas);
                    sprite = paintbrushClone.GetComponent<SpriteRenderer>();
                    sprite.color = colorChoosing;
                    sprite.sortingOrder = layer;
                    sprite.transform.position = newDrawPos;
                    layer++;
                    lastDrawPos = sprite.transform.position;
                }
            }
            else if (Input.GetMouseButton(0))
            {
                Working();
                if (renderMode == RenderMode.LineRender)
                {
                    if (line != null)
                    {
                        i++;
                        line.SetVertexCount(i);
                        line.SetPosition(i - 1, Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15)));
                    }
                }
                else if (renderMode == RenderMode.SpriteRender)
                {
                    newDrawPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 15));

                    /* Interpolation */
                    #region Interpolation
                    drawDistance = Vector3.Distance(lastDrawPos, newDrawPos);
                    //Debug.Log("Dist. : " + drawDistance);
                    numInterpolation = (int)(drawDistance / 0.5f) + 1;
                    //Debug.Log("Int.Po. : " + numInterpolation);
                    if (numInterpolation > 0)
                    {
                        //Debug.Log("Last: " + lastDrawPos);
                        for (int i = 0; i < numInterpolation; i++)
                        {
                            posInterpolation = sprite.transform.position + (newDrawPos - lastDrawPos) * (i + 1) / (numInterpolation + 1);
                            //Debug.Log("Inner " + i+ " : " + posInterpolation);
                            paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);
                            paintbrushClone.transform.SetParent(renderCanvas);
                            interpolationSprite = paintbrushClone.GetComponent<SpriteRenderer>();
                            interpolationSprite.color = colorChoosing;
                            interpolationSprite.sortingOrder = layer;
                            interpolationSprite.transform.position = posInterpolation;
                        }
                        //Debug.Log("Now: " + newDrawPos);
                    }
                    #endregion

                    paintbrushClone = (GameObject)Instantiate(paintbrushChoosing, paintbrushChoosing.transform.position, transform.rotation);
                    paintbrushClone.transform.SetParent(renderCanvas);
                    sprite = paintbrushClone.GetComponent<SpriteRenderer>();
                    sprite.color = colorChoosing;
                    sprite.sortingOrder = layer;
                    sprite.transform.position = newDrawPos;
                    layer++;
                    lastDrawPos = sprite.transform.position;
                }
            }
        }
    }

    #region Preload
    IEnumerator ReadCollections()
    {
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);
        yield return new WaitForSeconds(1.37f);
        for (int i = 0; i < collections.Length; i++)
        {
            bool success = false;
            try
            {
                Bitmap image = new Bitmap(folderPath + "Collections " + i + ".png");
                if (image != null)
                {
                    Texture2D t = new Texture2D(image.Width, image.Height);

                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            t.SetPixel(x, y,
                                  new Color32(
                                   image.GetPixel(x, image.Height - y - 1).R,
                                   image.GetPixel(x, image.Height - y - 1).G,
                                   image.GetPixel(x, image.Height - y - 1).B,
                                   image.GetPixel(x, image.Height - y - 1).A
                                   )
                            );
                        }
                    }
                    t.Apply();
                    collections[i] = t;
                }
                success = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e);
            }
            if (success)
            {
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
        timerAutoSave = Time.time + delayAutoSave;
        timerAutoShow = Time.time + delayAutoShow;
    }
    public void BurshChoosing(int orderBrush)
    {
        if (orderBrush == 0)
            renderMode = RenderMode.LineRender;
        else
            renderMode = RenderMode.SpriteRender;
        paintbrushChoosing = paintbrush[orderBrush];
        Working();
    }
    public void ColorChoosing(GameObject paintBucket)
    {
        colorChoosing = paintBucket.GetComponent<UnityEngine.UI.Image>().color;
        Working();
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
            if (orderSave == 20)
                orderSave = 0;
            saved = false;
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
        timerAutoSave = Time.time + delayAutoSave;
        //在擷取畫面之前請等到所有的Camera都Render完
        yield return new WaitForEndOfFrame();

        Texture2D texture = new Texture2D((int)recordWidth, (int)myCamera.pixelHeight);
        //擷取作畫範圍 parm: recordWidth = 1600 (e.g. 1920 x 1080)
        texture.ReadPixels(new Rect(0, 0, (int)recordWidth, (int)myCamera.pixelHeight), 0, 0, false);
        texture.Apply();

        /* Save */
        SaveTextureToFile(texture, "Collections " + orderSave);
        collections[orderSave] = texture;
        Debug.LogWarning("Save to Collections [" + orderSave + "]");
        orderShow = orderSave;
        saved = true;
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
    public void StartShow()
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
                play = true;
                break;
            }
        }
    }
    public void NextShow()
    {
        timerShow = Time.time + delayShow;
        while (collections[orderShow] == null)
        {
            orderShow++;
            if (orderShow == 20)
                orderShow = 0;
        }
        //Debug.Log("Show: " + orderShow);
        imgShow.texture = collections[orderShow];
        textOrder.text = "(" + (orderShow+1) + ")";
        orderShow++;
        if (orderShow == 20)
            orderShow = 0;
    }
    public void PlayPause()
    {
        play = !play;
        iconPause.SetActive(play);
        iconPlay.SetActive(!play);
        timerShow = Time.time + 1.0f;
    }
    public void PrevShow()
    {
        timerShow = Time.time + delayShow;
        orderShow -= 2;
        if (orderShow == -1)
            orderShow = 19;
        else if (orderShow == -2)
            orderShow = 18;
        while (collections[orderShow] == null)
        {
            orderShow--;
            if (orderShow == -1)
                orderShow = 19;
        }
        //Debug.Log("Show: " + orderShow);
        imgShow.texture = collections[orderShow];
        textOrder.text = "(" + (orderShow + 1) + ")";
        orderShow++;
        if (orderShow == 20)
            orderShow = 0;
    }
    #endregion

    #region Setting
    public void SetIntervalTime()
    {
        delayShow = intervalBar.value * 8 + 1;
        textIntervalShow.text = "" + (int)delayShow;
    }
    public void SetAutoSaveTime()
    {
        delayAutoSave = autoSaveBar.value * 15 + 15;
        textAutoSave.text = "" + (int)delayAutoSave;
    }
    public void SetAutoShowTime()
    {
        delayAutoShow = autoShowBar.value * 130 + 120;
        textAutoShow.text = "" + (int)delayAutoShow;
    }
    #endregion
}