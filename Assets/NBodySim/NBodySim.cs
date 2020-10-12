using UnityEngine;
using System.Collections;

namespace NBodySimProject
{

    //must be attached to a camera atm because DrawProcedural is used to render the points
    //You could move the script of the camera and just pass the buffers to the camera to be rendered in OnPostRender.
    [RequireComponent(typeof(Camera))]
    public class NBodySim : MonoBehaviour
    {

        const int READ = 0;
        const int WRITE = 1;
        const int DEFALUT_SIZE = 65536;

        public enum CONFIG { RANDOM, SHELL, EXPAND };

        public int m_seed = 0;
        public Material m_particleMat;
        public ComputeShader m_integrateBodies;
        public CONFIG m_config = CONFIG.EXPAND;
        public int m_numBodies = DEFALUT_SIZE;
        public float m_positionScale = 16.0f;
        public float m_velocityScale = 1.0f;
        public float m_damping = 0.96f;
        public float m_softeningSquared = 0.1f;
        public float m_speed = 1.0f;

        ComputeBuffer[] m_positions, m_velocities;

        //	m_numBodies: Sets the number of bodies in the simulation.  This 
        //	should be a multiple of 256.
        //		
        //	p: Sets the width of the tile used in the simulation.
        //	The default is 64.
        //
        //	q: Sets the height of the tile used in the simulation.
        //	The default is 4.
        //	
        //	Note: q is the number of threads per body, and p*q should be 
        //	less than or equal to 256.

        //p must match the value of NUM_THREADS in the IntegrateBodies shader
        int p = 64;
        int q = 4;

        void Start()
        {

            if (p * q > 256)
                Debug.Log("NBodySim::Start - p*q must be <= 256. Simulation will have errors.");

            if (m_numBodies % 256 != 0)
            {
                while (m_numBodies % 256 != 0)
                    m_numBodies++;

                Debug.Log("NBodySim::Start - numBodies must be a multiple of 256. Changing numBodies to " + m_numBodies);
            }

            m_positions = new ComputeBuffer[2];
            m_velocities = new ComputeBuffer[2];

            m_positions[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_positions[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            m_velocities[READ] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);
            m_velocities[WRITE] = new ComputeBuffer(m_numBodies, sizeof(float) * 4);

            Random.InitState(m_seed);

            //Option to choose from a few starting settings
            //Each gives a slightly different result
            switch ((int)m_config)
            {
                case (int)CONFIG.RANDOM:
                    ConfigRandom();
                    break;

                case (int)CONFIG.SHELL:
                    ConfigShell();
                    break;

                case (int)CONFIG.EXPAND:
                    ConfigExpand();
                    break;

                default:
                    ConfigExpand();
                    break;
            };


        }

        void ConfigRandom()
        {
            float scale = m_positionScale * Mathf.Max(1, m_numBodies / DEFALUT_SIZE);
            float vscale = m_velocityScale * scale;

            Vector4[] positions = new Vector4[m_numBodies];
            Vector4[] velocities = new Vector4[m_numBodies];

            int i = 0;
            while (i < m_numBodies)
            {
                Vector3 pos = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
                Vector3 vel = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));

                if (Vector3.Dot(pos, pos) > 1.0) continue;
                if (Vector3.Dot(vel, vel) > 1.0) continue;

                positions[i] = new Vector4(pos.x * scale, pos.y * scale, pos.z * scale, 1.0f);
                velocities[i] = new Vector4(vel.x * vscale, vel.y * vscale, vel.z * vscale, 1.0f);

                i++;
            }

            m_positions[READ].SetData(positions);
            m_positions[WRITE].SetData(positions);

            m_velocities[READ].SetData(velocities);
            m_velocities[WRITE].SetData(velocities);

        }

        void ConfigShell()
        {
            float scale = m_positionScale;
            float vscale = m_velocityScale * scale;
            float inner = 2.5f * scale;
            float outer = 4.0f * scale;

            Vector4[] positions = new Vector4[m_numBodies];
            Vector4[] velocities = new Vector4[m_numBodies];

            int i = 0;
            while (i < m_numBodies)
            {
                Vector3 point = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));

                if (point.magnitude > 1.0f) continue;

                positions[i] = new Vector3();
                positions[i].x = point.x * (inner + (outer - inner) * Random.value);
                positions[i].y = point.y * (inner + (outer - inner) * Random.value);
                positions[i].z = point.z * (inner + (outer - inner) * Random.value);
                positions[i].w = 1.0f;

                Vector3 axis = new Vector3(0, 0, 1);

                if (1.0f - Vector3.Dot(point, axis) < 1e-6f)
                {
                    axis.x = point.y;
                    axis.y = point.x;
                    axis.Normalize();
                }

                Vector3 vv = Vector3.Cross(positions[i], axis);

                velocities[i] = new Vector3();
                velocities[i].x = vv.x * vscale;
                velocities[i].y = vv.y * vscale;
                velocities[i].z = vv.z * vscale;
                velocities[i].w = 1.0f;

                i++;
            }

            m_positions[READ].SetData(positions);
            m_positions[WRITE].SetData(positions);

            m_velocities[READ].SetData(velocities);
            m_velocities[WRITE].SetData(velocities);
        }

        void ConfigExpand()
        {
            float scale = m_positionScale * Mathf.Max(1, m_numBodies / DEFALUT_SIZE);
            float vscale = m_velocityScale * scale;

            Vector4[] positions = new Vector4[m_numBodies];
            Vector4[] velocities = new Vector4[m_numBodies];

            int i = 0;
            while (i < m_numBodies)
            {
                Vector3 pos = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));

                if (Vector3.Dot(pos, pos) > 1.0) continue;

                positions[i] = new Vector4(pos.x * scale, pos.y * scale, pos.z * scale, 1.0f);
                velocities[i] = new Vector4(pos.x * vscale, pos.y * vscale, pos.z * vscale, 1.0f);

                i++;
            }

            m_positions[READ].SetData(positions);
            m_positions[WRITE].SetData(positions);

            m_velocities[READ].SetData(velocities);
            m_velocities[WRITE].SetData(velocities);
        }

        void Swap(ComputeBuffer[] buffer)
        {
            ComputeBuffer tmp = buffer[READ];
            buffer[READ] = buffer[WRITE];
            buffer[WRITE] = tmp;
        }


        void Update()
        {
            m_integrateBodies.SetFloat("_DeltaTime", Time.deltaTime * m_speed);
            m_integrateBodies.SetFloat("_Damping", m_damping);
            m_integrateBodies.SetFloat("_SofteningSquared", m_softeningSquared);
            m_integrateBodies.SetInt("_NumBodies", m_numBodies);
            m_integrateBodies.SetVector("_ThreadDim", new Vector4(p, q, 1, 0));
            m_integrateBodies.SetVector("_GroupDim", new Vector4(m_numBodies / p, 1, 1, 0));
            m_integrateBodies.SetBuffer(0, "_ReadPos", m_positions[READ]);
            m_integrateBodies.SetBuffer(0, "_WritePos", m_positions[WRITE]);
            m_integrateBodies.SetBuffer(0, "_ReadVel", m_velocities[READ]);
            m_integrateBodies.SetBuffer(0, "_WriteVel", m_velocities[WRITE]);

            m_integrateBodies.Dispatch(0, m_numBodies / p, 1, 1);

            Swap(m_positions);
            Swap(m_velocities);

        }

        void OnPostRender()
        {
            m_particleMat.SetPass(0);
            m_particleMat.SetBuffer("_Positions", m_positions[READ]);

            Graphics.DrawProceduralNow(MeshTopology.Points, m_numBodies);
        }

        void OnDestroy()
        {
            m_positions[READ].Release();
            m_positions[WRITE].Release();
            m_velocities[READ].Release();
            m_velocities[WRITE].Release();
        }

    }

}
















