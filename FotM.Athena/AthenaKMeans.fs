﻿namespace FotM.Athena

open Math
open FotM.Hephaestus.CollectionExtensions

type AthenaKMeans<'a>(featureExtractor: 'a -> float array, shouldNormalize: bool, applyMetric: bool) =

    let distance = squaredEuclideanDistance

    let ``kmeans++`` (matrix: float[][]) (k: int) (rng: System.Random) =

        let rec buildCentroids (centroids: Vector list) n =
            if n < k then 
                let distanceToClosestCentroid v = 
                    centroids 
                    |> List.map (distance v) 
                    |> List.min

                let distances = matrix |> Array.map distanceToClosestCentroid

                let border = rng.NextDouble() * Array.sum(distances)

                let rec nextCentroid runningSum i =
                    if runningSum < border then nextCentroid (runningSum + distances.[i]) (i+1)
                    else matrix.[i-1]
                
                let next = nextCentroid 0.0 0

                buildCentroids (next::centroids) (n + 1)
            else
                centroids

        buildCentroids [matrix |> Array.randomElement rng]  1

    let getPointsForCluster (clustering: int[]) (clusterNum: int) (matrix: Vector[])  =
        clustering
        |> Seq.mapi (fun i ci -> i, ci)
        |> Seq.filter (fun idx -> snd idx = clusterNum)
        |> Seq.map (fun idx -> matrix.[fst idx])
        |> Array.ofSeq

    let getClusterMean (centroid: Vector) (clusterPoints: Vector array) =
        if clusterPoints.Length = 0 then Array.zeroCreate centroid.Length else VectorOps.mean clusterPoints

    let cluster (nIteration: int) (k: int) (rng: System.Random) (matrix: float[][]): Vector[] * int[] =

        let n = matrix.[0].Length
        let maxGroupSize = matrix.Length / k

        let centroids = ``kmeans++`` matrix k rng

        let rec iterate (centroids: Vector list) (currentClustering: int[]) =
            let newClustering = matrix |> Array.map (fun input -> fst( centroids |> List.miniBy(distance input) ) )

            if newClustering <> currentClustering then
                let newCentroids = centroids |> List.mapi (fun i c -> matrix |> getPointsForCluster newClustering i |> getClusterMean c)
                iterate newCentroids newClustering
            else
                centroids |> List.toArray, currentClustering

        iterate centroids [||]

    let distortionMetric (matrix: float[][]) (centroids: Vector[], clustering: int[]) : float = 
        matrix
        |> Array.mapi (fun i row -> distance row centroids.[clustering.[i]])
        |> Array.sum

    let resultMetric (size: int) (matrix: float[][]) (centroids: Vector[], clustering: int[]) =
            let groups = clustering |> Seq.groupBy id
            let nGroups = groups |> Seq.length
            let nOverbooked = groups |> Seq.filter (fun g -> snd g |> Seq.length > size) |> Seq.length
            let nRegular = groups |> Seq.filter (fun g -> snd g |> Seq.length = size) |> Seq.length

            (
                nOverbooked,    // prioritize clusterings with less overbooked teams
                -nRegular,      // out of those prioritize clusterings with most teams of right size
                -nGroups,       // out of those prioritize clusterings with more total teams (that's for when we have overbooked teams at all)
                distortionMetric matrix (centroids, clustering) // out of those get whatever has smaller distortion
            )

    member this.computeGroups (dataSet: 'a array) (nGroups: int) =
        let input = dataSet |> Array.map featureExtractor
        let matrix = if shouldNormalize then normalize input else input

        let rng = System.Random()

        let groupSize = int (ceil (matrix.Length ./. nGroups))
            
        if applyMetric then
            let nClusteringIterations = 100

            let orderedResults = 
                [for i in 0..nClusteringIterations do yield matrix |> cluster 0 nGroups rng]
                |> List.sortBy (fun clustering -> clustering |> resultMetric groupSize matrix)

            snd orderedResults.Head
        else
            snd (matrix |> cluster 0 nGroups rng)

    interface FotM.Utilities.IKMeans<'a> with
        member this.ComputeGroups(dataSet, nGroups) = this.computeGroups dataSet nGroups          
