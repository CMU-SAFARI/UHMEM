# UHMEM
UHMEM is a cycle-accurate hybrid memory simulator. It models a hybrid memory system consisting of a fast memory component and a slow memory component with separate channels connected to processors. The simulator is described at:
Yang Li, Saugata Ghose, Jongmoo Choi, Jin Sun, Hui Wang, Onur Mutlu, "Utility-Based Hybrid Memory Management", in IEEE Cluster Computing Conference (Cluster), 2017. 

# Prerequisites
In order to run UHMEM, we need to install Microsoft Mono package on the system.

We also need to use the Pintools to collect applications' load/store traces, and then cache-filter these traces to get the memory reference traces. These traces should be put in the FilteredTrace folder with a format as follows:

Each line: 
[number of instructions between last memory reference and this memory reference] [the address of memory read reference] [the address of memory write reference (if there is any)]

# Getting Started
To build UHMEM, simply do:

make

To run the UHMEM mechanism proposed in [Li+, Cluster 2017], run the following command:

./pfa.sh

To run the prior RBLA or ALL mechanisms (see [Li+, Cluster 2017], run the following command:

./rbla.sh

./all.sh

The simulation results will be output to pfa.dat, rbla.dat, all.dat files.

# Contributors:
Yang Li (Carnegie Mellon University) 
