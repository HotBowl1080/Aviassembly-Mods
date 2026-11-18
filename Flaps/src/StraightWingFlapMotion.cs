using System;
using System.Collections.Generic;
using UnityEngine;

// Development implementation. The moving vertex set and hinge endpoints must
// come from the captured Straight Wing mesh, not a guessed vertex range.
internal sealed class StraightWingFlapMotion
{
    readonly Vector3[] restPositions,restNormals;
    readonly int[] moving;
    readonly Vector3 hinge,axis,aft;
    readonly float lastSettingAft,lastSettingDown;
    internal readonly Vector3[] positions,normals;

    internal StraightWingFlapMotion(Vector3[] vertices,Vector3[] vertexNormals,
        int[] flapVertexIds,Vector3 hingeRoot,Vector3 hingeTip,Vector3 aftDirection,
        float finalAftTravel=.0254f,float finalDownTravel=.0127f)
    {
        if(vertices==null || vertexNormals==null || vertices.Length!=vertexNormals.Length ||
            flapVertexIds==null || flapVertexIds.Length<3)
            throw new ArgumentException("Missing Straight Wing flap mesh data");
        if((hingeTip-hingeRoot).sqrMagnitude<1e-10f)
            throw new ArgumentException("Straight Wing flap hinge has no length");
        hinge=hingeRoot;axis=(hingeTip-hingeRoot).normalized;
        aft=Vector3.ProjectOnPlane(aftDirection,axis).normalized;
        if(aft.sqrMagnitude<.9f || finalAftTravel<0f || finalDownTravel<0f)
            throw new ArgumentException("Invalid Straight Wing flap motion");
        var ids=new HashSet<int>();
        foreach(int id in flapVertexIds)
        {
            if(id<0 || id>=vertices.Length)throw new ArgumentOutOfRangeException("flapVertexIds");
            ids.Add(id);
        }
        moving=new int[ids.Count];ids.CopyTo(moving);
        restPositions=(Vector3[])vertices.Clone();restNormals=(Vector3[])vertexNormals.Clone();
        positions=(Vector3[])vertices.Clone();normals=(Vector3[])vertexNormals.Clone();
        lastSettingAft=finalAftTravel;lastSettingDown=finalDownTravel;
    }
    internal StraightWingFlapMotion(StraightWingFlapMotion source)
    {
        restPositions=source.restPositions;restNormals=source.restNormals;moving=source.moving;
        hinge=source.hinge;axis=source.axis;aft=source.aft;
        lastSettingAft=source.lastSettingAft;lastSettingDown=source.lastSettingDown;
        positions=(Vector3[])source.positions.Clone();normals=(Vector3[])source.normals.Clone();
    }

    // Retain the existing selector's 0/10/20/30 detents. The additional
    // translation begins after setting 3 and reaches its small offset at 4.
    internal void Pose(float selectorDegrees,Vector3 localDown)
    {
        float angle=Mathf.Clamp(selectorDegrees,0f,30f);
        Vector3 down=Vector3.ProjectOnPlane(localDown,axis).normalized;
        if(down.sqrMagnitude<.9f)throw new ArgumentException("Down is parallel to the flap hinge");
        float sign=Vector3.Dot(Vector3.Cross(aft,down),axis)>=0f?1f:-1f;
        double radians=sign*angle*Math.PI/180.0;
        float cosine=(float)Math.Cos(radians),sine=(float)Math.Sin(radians);
        float blend=Mathf.Clamp01((angle-20f)/10f);
        blend=blend*blend*(3f-2f*blend);
        Vector3 travel=(aft*lastSettingAft+down*lastSettingDown)*blend;
        foreach(int id in moving)
        {
            Vector3 p=restPositions[id]-hinge,n=restNormals[id];
            positions[id]=hinge+p*cosine+Vector3.Cross(axis,p)*sine+axis*Vector3.Dot(axis,p)*(1f-cosine)+travel;
            normals[id]=(n*cosine+Vector3.Cross(axis,n)*sine+axis*Vector3.Dot(axis,n)*(1f-cosine)).normalized;
        }
    }
}
