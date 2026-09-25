#include "ufbx/ufbx.h"
#include <vector>
#include <memory>
#include <cmath>
#include <cstdint>
#include <climits>
struct Vertex {float f[16];};
struct Result {std::vector<Vertex> vertices;std::vector<uint32_t> indices;int colors=0;};
struct View {const Vertex* vertices;const uint32_t* indices;int vertex_count,index_count,colors;};
extern "C" __declspec(dllexport) void fuse_ufbx_free(void* p){delete static_cast<Result*>(p);}
// 0 success; 1 unsupported (use Assimp); 2 failed. All exceptions stay within this DLL.
extern "C" __declspec(dllexport) int fuse_ufbx_load(const void* bytes,size_t size,float scale,void** handle,View* view){
 *handle=nullptr;*view={};
 try {
 ufbx_load_opts opts={};ufbx_error error={};
 std::unique_ptr<ufbx_scene,decltype(&ufbx_free_scene)> scene(ufbx_load_memory(bytes,size,&opts,&error),ufbx_free_scene);if(!scene)return 2;
 if(scene->anim_stacks.count||scene->skin_deformers.count||scene->blend_deformers.count)return 1;
 auto result=std::make_unique<Result>();int colors=-1;
 for(size_t n=0;n<scene->nodes.count;n++){auto node=scene->nodes.data[n];auto m=node->mesh;if(!m)continue;
 if(m->color_sets.count>2||m->uv_sets.count>1||!m->vertex_normal.exists||ufbx_matrix_determinant(&node->geometry_to_world)<=0)return 1;
 if(colors<0)colors=(int)m->color_sets.count;if(colors!=(int)m->color_sets.count)return 1;
 if(result->vertices.size()+m->num_indices>INT_MAX)return 2;
 auto nm=ufbx_matrix_for_normals(&node->geometry_to_world);result->vertices.reserve(result->vertices.size()+m->num_indices);
 for(size_t fi=0;fi<m->faces.count;fi++){auto face=m->faces.data[fi];if(face.num_indices!=3)return 1;
 for(size_t k=0;k<3;k++){size_t ix=face.index_begin+k;
 auto p=ufbx_transform_position(&node->geometry_to_world,ufbx_get_vertex_vec3(&m->vertex_position,ix));auto normal=ufbx_transform_direction(&nm,ufbx_get_vertex_vec3(&m->vertex_normal,ix));double len=sqrt(normal.x*normal.x+normal.y*normal.y+normal.z*normal.z);if(len==0)len=1;
 ufbx_vec2 uv={};ufbx_vec4 c0={},c1={};if(m->vertex_uv.exists)uv=ufbx_get_vertex_vec2(&m->vertex_uv,ix);if(colors>0)c0=ufbx_get_vertex_vec4(&m->color_sets.data[0].vertex_color,ix);if(colors>1)c1=ufbx_get_vertex_vec4(&m->color_sets.data[1].vertex_color,ix);
 Vertex v={{(float)(p.x*scale),(float)(p.y*scale),(float)(p.z*scale),(float)(normal.x/len),(float)(normal.y/len),(float)(normal.z/len),(float)uv.x,(float)uv.y,(float)c0.x,(float)c0.y,(float)c0.z,(float)c0.w,(float)c1.x,(float)c1.y,(float)c1.z,(float)c1.w}};result->vertices.push_back(v);
 }}}
 if(result->vertices.empty())return 2;
 result->indices.resize(result->vertices.size());ufbx_vertex_stream stream={result->vertices.data(),result->vertices.size(),sizeof(Vertex)};
 size_t count=ufbx_generate_indices(&stream,1,result->indices.data(),result->indices.size(),nullptr,&error);if(!count)return 2;
 result->vertices.resize(count);result->colors=colors;
 *view={result->vertices.data(),result->indices.data(),(int)count,(int)result->indices.size(),colors};*handle=result.release();return 0;
 }catch(...){return 2;}
}
