using System;
using System.Runtime.CompilerServices;

namespace Inventory.Scripts.Core.Items
{
    [Serializable]
    public class Position
    {
        private int m_X;
        private int m_Y;

        /// <summary>
        ///   <para>X component of the vector.</para>
        /// </summary>
        public int x
        {
            [MethodImpl((MethodImplOptions)256)] get => this.m_X;
            [MethodImpl((MethodImplOptions)256)] set => this.m_X = value;
        }

        /// <summary>
        ///   <para>Y component of the vector.</para>
        /// </summary>
        public int y
        {
            [MethodImpl((MethodImplOptions)256)] get => this.m_Y;
            [MethodImpl((MethodImplOptions)256)] set => this.m_Y = value;
        }

        [MethodImpl((MethodImplOptions)256)]
        public Position(int x, int y)
        {
            m_X = x;
            m_Y = y;
        }
    }
}