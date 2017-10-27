#include <pcl/ModelCoefficients.h>
#include <pcl/point_types.h>
#include <pcl/io/pcd_io.h>
#include <pcl/io/obj_io.h>
#include <pcl/filters/extract_indices.h>
#include <pcl/filters/voxel_grid.h>
#include <pcl/features/normal_3d.h>
#include <pcl/kdtree/kdtree.h>
#include <pcl/sample_consensus/method_types.h>
#include <pcl/sample_consensus/model_types.h>
#include <pcl/segmentation/sac_segmentation.h>
#include <pcl/segmentation/extract_clusters.h>
#include <pcl/visualization/cloud_viewer.h>
#include <pcl/common/common_headers.h>

using namespace pcl;
using namespace std;


int main(int argc, char** argv)
{
    // Load Cloud
    PointCloud<PointXYZ>::Ptr cloudOriginal(new PointCloud<PointXYZ>);

    OBJReader reader;
    if (reader.read("SpatialMapping.obj", *cloudOriginal) == -1) {
        cout << "Cloud reading failed." << endl;
        return (-1);
    }

    cout << "cloudOriginal has " << cloudOriginal->points.size() << " points." << endl;


    // Filtering with VoxelGrid
    PointCloud<PointXYZ>::Ptr cloudFiltered(new PointCloud<PointXYZ>);

    VoxelGrid<PointXYZ> voxelGrid;
    voxelGrid.setInputCloud(cloudOriginal);
    voxelGrid.setLeafSize(0.01f, 0.01f, 0.01f);
    voxelGrid.filter(*cloudFiltered);

    cout << "cloudFiltered has " << cloudFiltered->points.size() << " points." << endl;

    // Segmentation
    PointCloud<PointXYZ>::Ptr cloudSegmentation(new PointCloud<PointXYZ>);

    SACSegmentation<PointXYZ> segmentation;
    PointIndices::Ptr inliers(new PointIndices);
    ModelCoefficients::Ptr coefficients(new ModelCoefficients);

    segmentation.setModelType(SACMODEL_PERPENDICULAR_PLANE);
    segmentation.setDistanceThreshold(0.2);
    segmentation.setMethodType(SAC_RANSAC);
    segmentation.setMaxIterations(1000); // ToDo: more?
    segmentation.setOptimizeCoefficients(true);
    segmentation.setEpsAngle(deg2rad(5.0));
    Eigen::Vector3f axis(0.0, 0.0, 1.0);
    segmentation.setAxis(axis);
    cout << "axis = " << axis.x() << " " << axis.y() << " " << axis.z() << endl;
    /*printf("\nsegAxis= (%d, %d, %d)\n", axis.x(), axis.y(), axis.z());*/

    Eigen::Vector3f segAxis;
    segAxis = segmentation.getAxis();
    //printf("\nsegAxis= (%d, %d, %d)\n", segAxis[0], segAxis[1], segAxis[2]);
    cout << "segAxis = " << segAxis.x() << " " << segAxis.y() << " " << segAxis.z() << endl;

    PointCloud<PointXYZ>::Ptr cloudSegmented(new PointCloud<PointXYZ>);
    PointCloud<PointXYZ>::Ptr cloudF(new PointCloud<PointXYZ>);
    ExtractIndices<PointXYZ> extractIndices;
    ExtractIndices<Normal> extractNormals;

    int numPoints = (int)cloudFiltered->points.size();
    while (cloudFiltered->points.size() > 0.3 * numPoints) {
        segmentation.setInputCloud(cloudFiltered);
        segmentation.segment(*inliers, *coefficients);
        if (inliers->indices.size() == 0) {
            cout << "Couldn't estimate planar model" << endl;
        }

        extractIndices.setInputCloud(cloudFiltered);
        extractIndices.setIndices(inliers);
        extractIndices.setNegative(false);
        extractIndices.filter(*cloudSegmentation);

        cout << "cloudSegmentation has " << cloudSegmentation->points.size() << " points." << endl;
        *cloudSegmented += *cloudSegmentation;

        extractIndices.setNegative(true);
        extractIndices.filter(*cloudF);
        cloudFiltered.swap(cloudF);
        cout << "cloudFiltered has " << cloudFiltered->points.size() << " points left." << endl;
        
        visualization::PCLVisualizer viewer("Segment highlight");
        visualization::PointCloudColorHandlerCustom<PointXYZ> cloudFilteredColorHandler(cloudFiltered, 255, 255, 255);
        visualization::PointCloudColorHandlerCustom<PointXYZ> cloudSegmentationColorHandler(cloudSegmentation, 230, 20, 20);

        viewer.addPointCloud(cloudFiltered, cloudFilteredColorHandler, "cloudFiltered");
        viewer.addPointCloud(cloudSegmentation, cloudSegmentationColorHandler, "cloudSegmentation");

        viewer.addCoordinateSystem(1.0, "cloud", 0);
        viewer.setBackgroundColor(0.05, 0.05, 0.05, 0); // Setting background to a dark grey
        viewer.setPointCloudRenderingProperties(pcl::visualization::PCL_VISUALIZER_POINT_SIZE, 2, "cloudFiltered");
        viewer.setPointCloudRenderingProperties(pcl::visualization::PCL_VISUALIZER_POINT_SIZE, 2, "cloudSegmentation");

        while (!viewer.wasStopped())
        {
            viewer.spinOnce();
        }
    }
    
    //// Creating the KdTree object for the search method of the extraction
    //pcl::search::KdTree<pcl::PointXYZ>::Ptr tree(new pcl::search::KdTree<pcl::PointXYZ>);
    //tree->setInputCloud(cloudFiltered);

    //std::vector<pcl::PointIndices> cluster_indices;
    //pcl::EuclideanClusterExtraction<pcl::PointXYZ> ec;
    //ec.setClusterTolerance(0.02); // 2cm
    //ec.setMinClusterSize(100);
    //ec.setMaxClusterSize(25000);
    //ec.setSearchMethod(tree);
    //ec.setInputCloud(cloudFiltered);
    //ec.extract(cluster_indices);
    //
    //cout << cluster_indices.size() << endl;
    //PCDWriter writer;
    //int j = 0;
    //for (vector<PointIndices>::const_iterator it = cluster_indices.begin(); it != cluster_indices.end(); ++it) {
    //    cout << "ding!" << endl;
    //    PointCloud<PointXYZ>::Ptr cluster(new PointCloud<PointXYZ>);

    //    for (vector<int>::const_iterator pit = it->indices.begin(); pit != it->indices.end(); ++pit) {
    //        cluster->points.push_back(cloud->points[*pit]);
    //    }
    //    cluster->width = cluster->points.size();
    //    cluster->height = 1;
    //    cluster->is_dense = true;

    //    std::cout << "PointCloud representing the Cluster: " << cluster->points.size() << " data points." << std::endl;
    //    std::stringstream ss;
    //    ss << "cloud_cluster_" << j << ".pcd";
    //    writer.write<PointXYZ>(ss.str(), *cluster, false); //*
    //    j++;
    //}


    visualization::CloudViewer viewer("Cloud Viewer");
    viewer.showCloud(cloudSegmented);
    while (!viewer.wasStopped())
    {
    }

    return 0;
}