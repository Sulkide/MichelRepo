using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ChildSpriteFollower : MonoBehaviour
{
    [Header("Références")]
    public SpriteRenderer parentSpriteRenderer; 

    [Header("Enfant généré")]
    public string childName = "ChildSprite";
    public Vector3 localOffset = new Vector3(0f, 0f, 0.1f);
    public int sortingOrderOffset = -1;

    [Header("Matérial de l'enfant")]
    public bool overrideMaterial = false;
    public Material childMaterialOverride;        

    private SpriteRenderer childSpriteRenderer;
    
    private Sprite _lastSprite;
    private bool _lastFlipX;
    private bool _lastFlipY;

    private void Awake()
    {
        if (parentSpriteRenderer == null)
            parentSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        CreateChild();
        SyncSpriteNow(); 
    }

    private void CreateChild()
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform);
        child.transform.localPosition = localOffset;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;   
        
        childSpriteRenderer = child.AddComponent<SpriteRenderer>();

        childSpriteRenderer.sortingLayerID = parentSpriteRenderer.sortingLayerID;
        childSpriteRenderer.sortingOrder = parentSpriteRenderer.sortingOrder + sortingOrderOffset;

        if (overrideMaterial && childMaterialOverride != null)
        {
            childSpriteRenderer.material = childMaterialOverride;
        }
        else
        {
            childSpriteRenderer.material = parentSpriteRenderer.material;
        }
    }

    private void LateUpdate()
    {
        if (parentSpriteRenderer == null || childSpriteRenderer == null)
            return;
        
        if (parentSpriteRenderer.sprite != _lastSprite
            || parentSpriteRenderer.flipX != _lastFlipX
            || parentSpriteRenderer.flipY != _lastFlipY)
        {
            SyncSpriteNow();
        }
    }

    private void SyncSpriteNow()
    {
        _lastSprite = parentSpriteRenderer.sprite;
        _lastFlipX = parentSpriteRenderer.flipX;
        _lastFlipY = parentSpriteRenderer.flipY;

        childSpriteRenderer.sprite = _lastSprite;
        childSpriteRenderer.flipX = _lastFlipX;
        childSpriteRenderer.flipY = _lastFlipY;
        // childSpriteRenderer.color = parentSpriteRenderer.color;
    }
}
