#pragma once
#include "ReflectionSceneResourceInput.h"
#include "ReflectionCaptureCulling.h"
#include <algorithm>
#include <list>
#include <set>
namespace San14::Render::Reflection {
// Scene-session owner. Resource declarations are prepared once. Accepted World
// owners publish their real complete roster, independently of Main visibility.
// Device allocations and successful capture receipts belong to the GPU owner.
class SceneFrameOwner {
    // F37E80 supplies 5000 to D695D0/D69760 independently of the resource's
    // MaxPrimitiveNum. This is the current callback's real pointer-list owner.
    static constexpr std::uint32_t FieldCallbackPointerCapacity=5000;
    struct RuntimeField {FieldDefinition definition;std::uint32_t flags=0x201;std::int32_t next{};};
    struct Bounds {Float3 lo{},hi{};bool operator==(const Bounds&)const=default;};
    struct Entry {Bounds bounds;std::uint32_t cell{},frameIndex{};std::uint64_t seen{};};
    SceneSource source_;
    bool prepared_{};
    std::uint64_t generation_{},resourceGeneration_{};
    std::vector<RuntimeField> fields_;
    std::map<std::string,Entry> entries_;
    std::map<std::uint32_t,std::list<std::string>> cells_;
    std::map<std::uint32_t,std::uint32_t> subtree_;
    std::shared_ptr<SceneFrame> frame_;
public:
    void prepare(const std::filesystem::path& directory){
        invalidate();entries_.clear();cells_.clear();subtree_.clear();fields_.clear();
        source_=readSceneSource(directory/"scene.bin");prepared_=true;++resourceGeneration_;
        for(const auto& field:source_.fields){
            if(!field.dimensions[0]||!field.dimensions[1]||!field.dimensions[2]||!field.cellSize||!field.tileSize||!field.gBufSize)
                throw std::runtime_error("Actual Reflection Field declaration has an unusable grid/storage shape");
            fields_.push_back({field,0x201,0});
        }
    }
    const SceneSource& source()const {if(!prepared_)throw std::runtime_error("Scene resources were not prepared");return source_;}
    void invalidate()noexcept{if(frame_&&frame_->lifetime)frame_->lifetime->current=false;frame_.reset();}
    ~SceneFrameOwner(){invalidate();}
    const std::shared_ptr<SceneFrame>& current()const {if(!frame_)throw std::runtime_error("Reflection Scene frame has not been published");frame_->requireCurrent();return frame_;}
    template<class ClipState> void publishClipPlanes(const ClipState& actual){
        auto& f=*current();std::array<Float4,6> planes;
        for(unsigned i=0;i<6;++i){const auto p=actual.shaderPlane(i);planes[i]={p.X,p.Y,p.Z,p.W};}f.actualClipPlanes=planes;
    }
    // The caller is the actual GPU submit tail. A failed/no-op submission must
    // never call this method. Ordinal equality is checked against this frame.
    void completeCaptureSubmission(std::uint32_t fieldId,std::uint32_t ordinal){
        auto& f=*current();auto work=std::find_if(f.captureWork.begin(),f.captureWork.end(),[&](const auto& w){return w.fieldObjectId==fieldId&&w.sourceOrdinal==ordinal;});
        if(work==f.captureWork.end())throw std::runtime_error("Capture receipt does not name this actual current Field work");
        auto& field=findField(fieldId);std::uint32_t next=ordinal+1u,population=field.definition.dimensions[2]*field.definition.dimensions[1]*field.definition.dimensions[0];
        field.next=next==population?-1:std::bit_cast<std::int32_t>(next);
        f.captureWork.erase(work);
    }
    void resetCaptureCursorAfterStorageRebuilt(std::uint32_t fieldId){findField(fieldId).next=0;}
    void setFieldPosition(std::uint32_t fieldId,const Float3& actualLocalPosition){auto& f=findField(fieldId);f.definition.origin=actualLocalPosition;f.flags=(f.flags&0xFFFFFFF9u)|1u;}
    void publish(const PreparedMapView& view,const source_camera::Frame& camera,
        const StaticModelResources& statics,const BaseRenderWorld& bases,const ArmyRenderWorld& armies,const ShadowPass& shadows,const NativeModelCollectionCaller& fieldCaller){
        if(!prepared_)throw std::runtime_error("Reflection Scene resources were not prepared");invalidate();
        auto f=std::make_shared<SceneFrame>();f->generation=++generation_;f->resourceGeneration=resourceGeneration_;f->viewSlot=view.slot();f->preparedView=&view;
        f->lifetime=std::make_shared<FrameLifetime>();f->commonFlags14=source_.commonFlags14;
        const auto& catalog=statics.catalog();
        std::map<std::uint32_t,std::uint64_t> staticKeys;for(const auto& d:statics.currentCandidates().draws)staticKeys.try_emplace(d.primitiveIndex,d.distanceKey);
        for(const auto& child:source_.children){
            if(child.classId!=0xC4B9B28D)continue;
            if(child.staticInstanceIndex==0xFFFFFFFFu){f->unresolvedModelObjectIds.push_back(child.objectId);continue;}
            const auto ordinal=child.staticInstanceIndex;if(ordinal>=catalog.instances.size())throw std::runtime_error("Reflection source ModelOrdinal exceeds actual static owner");
            const auto& instance=catalog.instances[ordinal];ModelReference model;model.family=ModelFamily::Static;
            model.identity="resource/"+std::to_string(source_.rootObjectId)+"/"+std::to_string(child.objectId);model.bounds=instance.obb;
            model.spatialFlags=std::uint32_t(child.flags)|((child.nodeFlags44&1)?0u:0x80000000u);
            model.opacity=child.opacity;model.eligible=child.eligibility!=0;model.captureAdmissionKnown=true;
            model.captureViewMask=child.world328&fieldCaller.firstMask;model.incompleteGeometry=instance.hasUnsupported!=0;
            if(auto key=shadows.currentReflectionPriorTupleKey(false,ordinal);key)for(std::uint32_t j=instance.firstPrimitive;j<instance.firstPrimitive+instance.primitiveCount;j++)staticKeys.try_emplace(j,*key);
            appendContinuous(*f,model,ordinal,instance,catalog,statics.geometry(),staticKeys);publishModel(*f,std::move(model));
        }
        const auto& primary=bases.primary();const auto& p=primary.source;
        std::map<std::uint32_t,std::uint64_t> primaryKeys;for(const auto& d:bases.plan().models.draws)primaryKeys.try_emplace(d.primitiveIndex,d.distanceKey);
        for(std::uint32_t i=0;i<p.catalog.instances.size();++i){if(!p.present.at(i))continue;const auto& instance=p.catalog.instances[i];const auto& admission=p.worlds.at(i);
            ModelReference model;model.family=ModelFamily::Primary;model.identity=p.identities.at(i);model.bounds=instance.obb;
            model.opacity=admission.opacityB0;model.eligible=admission.eligibility!=0;model.captureAdmissionKnown=true;model.captureViewMask=admission.world328&p.caller.firstMask;model.incompleteGeometry=instance.hasUnsupported!=0;
            if(auto key=shadows.currentReflectionPriorTupleKey(true,i);key)for(std::uint32_t j=instance.firstPrimitive;j<instance.firstPrimitive+instance.primitiveCount;j++)primaryKeys.try_emplace(j,*key);
            appendContinuous(*f,model,i,instance,p.catalog,primary.currentModels,primaryKeys);publishModel(*f,std::move(model));
        }
        if(const auto* army=armies.completeReflectionPopulation()){
            const auto* plan=armies.currentReflectionPlan();const auto* roster=armies.currentReflectionRoster();
            std::map<std::uint32_t,std::string> names;for(const auto& [key,index]:roster->actorIndices)names.emplace(index,key.identity());
            std::map<std::uint32_t,std::uint64_t> keys;for(const auto& d:plan->draws)keys.try_emplace(d.primitive,d.distanceKey);
            for(std::uint32_t i=0;i<army->source.actors.size();++i){const auto& a=army->source.actors[i];
                auto n=names.find(i);if(n==names.end())throw std::runtime_error("Current Army physical model has no actual World owner identity");
                ModelReference model;model.family=ModelFamily::Army;model.identity=n->second;model.bounds=a.obb;model.opacity=a.slotScalar;
                // Army's source physical roster does not serialize C394's World
                // masks. Retain the real model; refuse only a reached capture.
                model.eligible=true;model.captureAdmissionKnown=false;
                for(std::uint32_t j=0;j<army->source.primitives.size();++j){const auto& q=army->source.primitives[j];if(q.actor!=i)continue;
                    PrimitiveReference primitive;primitive.family=ModelFamily::Army;primitive.modelIndex=i;primitive.sourcePrimitiveIndex=j;
                    primitive.sourceDrawIndex=q.draw;primitive.templateIndex=q.sourceTemplate;primitive.tableOwner=q.tableOwner;
                    primitive.geometry=&army->currentModels.draws.at(j);
                    if(primitive.geometry->retainedVs)primitive.currentVertex=std::span<const char>(*primitive.geometry->retainedVs);else primitive.currentVertex=std::span<const char>(army->source.vertices.at(j));
                    if(auto k=keys.find(j);k!=keys.end())primitive.actualPriorTupleKey=k->second;
                    model.primitives.push_back(std::uint32_t(f->primitives.size()));f->primitives.push_back(std::move(primitive));
                }publishModel(*f,std::move(model));
            }
        }
        retireMissing();
        // Actual registered Field callbacks publish a root even when their
        // capture start is negative. Work count is never a root-presence test.
        for(auto& actual:fields_){auto& d=actual.definition;const auto& child=source_.children.at(d.rootIndex);
            std::uint32_t mode=(actual.flags>>8)&3u;if(mode==2)mode=source_.view0CaptureMode;
            const std::uint32_t request=actual.flags&7u;std::int32_t start=-1;
            if(mode==0)start=request?std::bit_cast<std::int32_t>(request-1):actual.next;
            // Fresh CPU count70 is zero. Mode1 cannot select a manual ordinal.
            if(request)actual.flags=((actual.flags+1u)&3u)|(actual.flags&0xFFFFFFF8u);
            FieldState state;state.definition=d;state.managerPosition={d.origin[0],d.origin[1],d.origin[2],1};state.requestFlags=actual.flags;
            state.registeredViews=0xFFFFFFFFu;state.capturePointerCapacity=FieldCallbackPointerCapacity;state.currentStart=start;state.wrapperNext=actual.next;f->fields.push_back(state);
            if((d.groupMask&0x7FFFu&(1u<<f->viewSlot))==0)continue;
            f->rootFieldObjectIds.push_back(d.objectId);
        }
        frame_=f; // Real roots are published before any later capture work can fail.
        for(const auto& state:f->fields){const auto& d=state.definition;const auto start=state.currentStart;
            if(std::find(f->rootFieldObjectIds.begin(),f->rootFieldObjectIds.end(),d.objectId)==f->rootFieldObjectIds.end()||start<0)continue;
            if(!f->unresolvedModelObjectIds.empty())throw std::runtime_error("Actual capture reaches unsupported root Model declarations; native source boundaries must be resolved");
            CaptureWork work;work.fieldObjectId=d.objectId;work.sourceOrdinal=std::uint32_t(start);work.position=cellPosition(d,work.sourceOrdinal);
            work.topDownEye=work.position;work.topDownEye[1]=source_camera::A(source_camera::A(d.viewRange,d.viewRange),work.position[1]);
            const source_camera::V4 center{work.position[0],work.position[1],work.position[2],work.position[3]};CaptureCulling culling(d,center,camera);
            for(auto modelIndex:traverse(*f,camera,d)){auto& model=f->models[modelIndex];
                if(model.captureAdmissionKnown&&(!model.eligible||model.opacity<=0||!(model.captureViewMask&(1u<<f->viewSlot))))continue;
                const auto visible=culling.test(model.bounds);if(!visible)continue;
                if(model.incompleteGeometry)throw std::runtime_error("Actual capture reaches a source Model with unresolved primitive profiles");
                if(!model.captureAdmissionKnown)throw std::runtime_error("Actual Army capture requires its current ModelWorld capture admission masks");
                for(unsigned face=0;face<6;face++){if(!(visible&(1u<<face)))continue;auto& output=work.faces[face].primitiveIndices;
                    if(output.size()+model.primitives.size()>=state.capturePointerCapacity)continue; // Source strict pointer capacity; depth already accumulated.
                    for(auto primitive:model.primitives){auto& tuple=f->primitives[primitive];
                        if(!tuple.actualPriorTupleKey)tuple.actualPriorTupleKey=source_shadow_camera::firstDistanceKey(culling.currentDistance(face));
                        output.push_back(primitive);}
                }
            }
            culling.finish(work);
            for(auto& face:work.faces)for(auto field:shadows.currentReflectionSortFields()){
                if(field!=14&&field!=3)throw std::runtime_error("Reached capture Shadow sort requires an unsupported current tuple field");
                std::stable_sort(face.primitiveIndices.begin(),face.primitiveIndices.end(),[&](auto a,auto b){
                    return field==14?*f->primitives[a].actualPriorTupleKey<*f->primitives[b].actualPriorTupleKey:f->primitives[a].tableOwner<f->primitives[b].tableOwner;});}
            f->captureWork.push_back(std::move(work));
        }
        frame_=std::move(f);
    }
private:
    RuntimeField& findField(std::uint32_t id){auto f=std::find_if(fields_.begin(),fields_.end(),[&](const auto& r){return r.definition.objectId==id;});if(f==fields_.end())throw std::runtime_error("Reflection Field owner was retired");return *f;}
    static Float4 cellPosition(const FieldDefinition& d,std::uint32_t ordinal){
        const auto plane=d.dimensions[0]*d.dimensions[1];if(!plane)throw std::runtime_error("Reflection actual grid division wraps to zero");
        const float v=float(std::bit_cast<std::int32_t>(ordinal));const auto y=std::int32_t(std::fmod(double(v/float(d.dimensions[0])),double(d.dimensions[1])));
        const auto z=std::int32_t(std::floor(v/float(plane)));using source_camera::A;using source_camera::M;
        return {A(M(d.cellSize,float(ordinal%d.dimensions[0])),d.origin[0]),A(M(d.cellSize,float(y)),d.origin[1]),A(M(d.cellSize,float(z)),d.origin[2]),1};
    }
    static void appendContinuous(SceneFrame& f,ModelReference& model,std::uint32_t ordinal,const NativeModelContinuousInstance& instance,
        const NativeModelContinuous& catalog,const NativeModelInputs& geometry,const std::map<std::uint32_t,std::uint64_t>& keys){
        for(std::uint32_t j=instance.firstPrimitive;j<instance.firstPrimitive+instance.primitiveCount;++j){const auto& q=catalog.primitives.at(j);
            PrimitiveReference r;r.family=model.family;r.modelIndex=ordinal;r.sourcePrimitiveIndex=j;r.sourceDrawIndex=q.drawIndex;r.templateIndex=q.templateIndex;r.tableOwner=q.tableOwner;
            r.geometry=&geometry.draws.at(j);r.currentVertex=std::span<const char>(q.vertex);if(auto k=keys.find(j);k!=keys.end())r.actualPriorTupleKey=k->second;
            model.primitives.push_back(std::uint32_t(f.primitives.size()));f.primitives.push_back(std::move(r));}
    }
    static Bounds worldBounds(const std::array<float,20>& obb){const auto b=source_shadow_camera::worldBox(obb);using source_camera::A;using source_camera::S;
        return {{S(b[12],b[0]),S(b[13],b[5]),S(b[14],b[10])},{A(b[12],b[0]),A(b[13],b[5]),A(b[14],b[10])}};}
    std::uint32_t morton(const Float3& point)const {std::array<std::uint32_t,3> q;for(unsigned i=0;i<3;i++){
        const float v=source_camera::M(source_camera::S(point[i],source_.position[i]),source_camera::D(64.f,source_.scale[i]));q[i]=v<63?(v>=0?std::uint32_t(v):0):63;}
        std::uint32_t result=0;for(unsigned i=0;i<6;i++)result|=((q[0]>>i&1)|((q[1]>>i&1)<<1)|((q[2]>>i&1)<<2))<<(i*3);return result;}
    std::uint32_t cell(const Bounds& b)const {auto first=morton(b.lo),last=morton(b.hi),path=first;unsigned depth=6;
        for(unsigned level=1;level<=6;level++)if(((first^last)>>(18-level*3)&7)!=0){depth=level-1;path=first>>(21-level*3);break;}
        unsigned offset=0,power=1;for(unsigned i=0;i<depth;i++){offset+=power;power*=8;}return offset+path;}
    void insert(const std::string& name,Entry& e){cells_[e.cell].push_front(name);for(auto c=e.cell;;c=(c-1)>>3){++subtree_[c];if(!c)break;}}
    void remove(const std::string& name,const Entry& e){auto& list=cells_.at(e.cell);list.remove(name);if(list.empty())cells_.erase(e.cell);
        for(auto c=e.cell;;c=(c-1)>>3){auto i=subtree_.find(c);if(!--i->second)subtree_.erase(i);if(!c)break;}}
    void publishModel(SceneFrame& f,ModelReference model){const auto index=std::uint32_t(f.models.size());const auto bounds=worldBounds(model.bounds);auto i=entries_.find(model.identity);
        if(i==entries_.end()){
            i=entries_.emplace(model.identity,Entry{bounds,cell(bounds),index,generation_}).first;insert(i->first,i->second);
        }else {auto& e=i->second;if(!(e.bounds==bounds)){remove(i->first,e);e.bounds=bounds;e.cell=cell(bounds);insert(i->first,e);}e.frameIndex=index;e.seen=generation_;}
        f.models.push_back(std::move(model));}
    void retireMissing(){for(auto i=entries_.begin();i!=entries_.end();){if(i->second.seen==generation_){++i;continue;}remove(i->first,i->second);i=entries_.erase(i);}}
    static bool overlaps(const Bounds& a,const Bounds& b){for(unsigned i=0;i<3;i++)if(a.hi[i]<b.lo[i]||b.hi[i]<a.lo[i])return false;return true;}
    std::vector<std::uint32_t> traverse(const SceneFrame& f,const source_camera::Frame& camera,const FieldDefinition&)const{
        using namespace source_shadow_camera;
        const auto direction=normalize(sub(camera.target,camera.eye));const auto view=lightView(camera.eye,direction,camera.up);
        Matrix basis{view[0],view[4],view[8],0,view[1],view[5],view[9],0,direction.X,direction.Y,direction.Z,direction.W,camera.eye.X,camera.eye.Y,camera.eye.Z,camera.eye.W};
        const float tangent=source_camera::tangentSse(M(camera.optics.viewSpan,.5f));
        const auto main=frustum(basis,camera.optics.nearPlane,camera.optics.farPlane,M(tangent,camera.optics.aspectRatio),tangent);
        Bounds search;search.lo.fill(std::numeric_limits<float>::max());search.hi.fill(-std::numeric_limits<float>::max());
        for(const auto& v:main.vertices){const Float3 point{v.X,v.Y,v.Z};for(unsigned i=0;i<3;i++){search.lo[i]=min(search.lo[i],point[i]);search.hi[i]=max(search.hi[i],point[i]);}}
        std::vector<Bounds> fieldBoxes;
        for(const auto id:f.rootFieldObjectIds){const auto i=std::find_if(f.fields.begin(),f.fields.end(),[&](const auto& r){return r.definition.objectId==id;});
            if(i==f.fields.end())throw std::runtime_error("Current Field root lost its actual source state");const auto& d=i->definition;Bounds box;
            for(unsigned k=0;k<3;k++){box.lo[k]=S(d.origin[k],d.viewRange);box.hi[k]=A(A(d.origin[k],M(float(std::bit_cast<std::int32_t>(d.dimensions[k])),d.cellSize)),d.viewRange);
                search.lo[k]=min(search.lo[k],box.lo[k]);search.hi[k]=max(search.hi[k],box.hi[k]);}fieldBoxes.push_back(box);}
        std::vector<std::uint32_t> output;
        auto visit=[&](std::uint32_t c){if(auto list=cells_.find(c);list!=cells_.end())for(const auto& key:list->second){const auto& e=entries_.at(key);
            bool inMain=true;
            for(const auto& plane:camera.planes){const auto q=plane.equation;const Float3 n{q.X,q.Y,q.Z};float dot=q.W;
                for(unsigned k=0;k<3;k++)dot=A(dot,M(n[k],n[k]<0?e.bounds.hi[k]:e.bounds.lo[k]));if(!(dot<=0)){inMain=false;break;}}
            bool fieldVisible=false;for(const auto& box:fieldBoxes)if(overlaps(e.bounds,box)){fieldVisible=true;break;}
            if(c==0||(overlaps(e.bounds,search)&&inMain)||fieldVisible)output.push_back(e.frameIndex);
        }};
        auto descend=[&](auto&& self,std::uint32_t c)->void{visit(c);unsigned depth=0;for(auto n=c;n;n=(n-1)>>3)++depth;if(depth==6)return;
            for(unsigned child=0;child<8;child++){const auto next=c*8+1+child;if(subtree_.contains(next))self(self,next);}};
        const auto start=cell(search);for(auto ancestor=start;ancestor;){ancestor=(ancestor-1)>>3;visit(ancestor);}descend(descend,start);return output;
    }
};
} // namespace San14::Render::Reflection
