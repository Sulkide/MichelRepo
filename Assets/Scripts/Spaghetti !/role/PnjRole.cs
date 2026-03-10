using UnityEngine;

public class PnjRole : EntityRole
{
    public override EntityType RoleType => EntityType.Pnj;

    [Header("PNJ Params")]
    public float talkRadius = 3f;

    public override void Tick(float dt)
    {
        //a faire
        
    }
}