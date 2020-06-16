# Sugiyama method (top down)

1. Cycle removal
   1. Nyehehe, don't have to deal with this because Visject graphs aren't allowed to have cycles 
2. Layer Assignment
   1. Dummy vertices places where a node is missing
   2. Every node belongs to a layer (layer 1, 2, 3, 4)
   3. e.g. Longest path layering, minimize height
      1. Nu! We'll end up with a super thicc node graph. We want something balanced
      2. *Though, for starters, this is fine!*
   4. e.g. Layering to minimize width
      1. NP hard
      2. e.g. Network Simplex Layering seems decent
      3. e.g. Layering with given width: Coffman-Graham algorithm
   5. e.g. Minimize the number of dummy vertices
      1. This in turn minimizes the length of connections
   6. Here we can enforce things like aligning similar subtrees (e.g. two float nodes into a multiply)
3. Crossing Reduction
   1. NP complete
   2. A bit different, because some orders are fixed (e.g. the two inputs of a multiply node have a fixed order)
   3. *For starters, just use the given order. As in, starting from the root, the order of nodes in a layer is fixed!*
      1. Iterate over a graph "layer by layer" aka breadth first
4. Horizontal Coordinate Assignment
   1. Put nodes on screen
   2. One coordinate is fixed (layer)
   3. The other coordinate can be moved a bit
   4. Here we can enforce things like [a node with two inputs should be aligned with the first input](https://twitter.com/joewintergreen/status/1212954361036279808)



## Warnings

1. Keeping the order similar to what the user did might be possible, but after multiple iterations...
2. The algorithms has to return very consistent results. It definitely shouldn't be random and adding/removing a few vertices shouldn't significantly change the result
3. It should be "comment aware" and thus keep nodes inside the comments
4. Formatting parts of a graph should be possible. 
   1. So, we have to be able to format stuff using width and height constraints (and the algorithm tries its best to fulfill those constraints)
   2. Or maybe, people prefer it when the formatter moves the other nodes?

