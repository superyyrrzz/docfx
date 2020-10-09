// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Markdig.Syntax;

namespace Microsoft.DocAsCode.MarkdigEngine.Extensions
{
    public class BlockAggregateContext
    {
        private readonly ContainerBlock _blocks;
        private int _currentBlockIndex = -1;

        public BlockAggregateContext(ContainerBlock blocks)
        {
            _blocks = blocks;
        }

        public Block CurrentBlock
        {
            get
            {
                if (_currentBlockIndex < 0 || _currentBlockIndex > _blocks.Count)
                {
                    return null;
                }

                return _blocks[_currentBlockIndex];
            }
        }

        public void AggregateTo(Block block, int blockCount)
        {
            if (block != _blocks[_currentBlockIndex])
            {
                _blocks[_currentBlockIndex] = block;
            }
            RemoveRange(_currentBlockIndex + 1, blockCount - 1);
            _currentBlockIndex = -1;
        }

        public Block LookAhead(int offset)
        {
            var index = _currentBlockIndex + offset;
            if (index >= _blocks.Count)
            {
                return null;
            }

            return _blocks[index];
        }

        internal bool NextBlock()
        {
            _currentBlockIndex++;
            return _currentBlockIndex < _blocks.Count;
        }

        private void RemoveRange(int index, int count)
        {
            while (count > 0)
            {
                count--;
                _blocks.RemoveAt(index);
            }
        }
    }
}
