#pragma once
#include "ReflectionCaptureTargets.h"

namespace San14::Render::Reflection {
struct FieldSamplers {
    ComPtr<ID3D11SamplerState> cube,octmap,radianceSingle,radianceMipped,shadow;
    static ComPtr<ID3D11SamplerState> loadSource(ID3D11Device* device,const fs::path& file) {
        auto bytes=load(file);if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Reflection source sampler descriptor size");
        D3D11_SAMPLER_DESC desc{};std::memcpy(&desc,bytes.data(),sizeof(desc));ComPtr<ID3D11SamplerState> result;
        check(device->CreateSamplerState(&desc,&result),"Reflection original sampler");return result;
    }
    void prepare(ID3D11Device* device,const fs::path& directory) {
        cube=loadSource(device,directory/"cube.bin");octmap=loadSource(device,directory/"octmap.bin");
        radianceSingle=loadSource(device,directory/"radiance-single.bin");radianceMipped=loadSource(device,directory/"radiance-mipped.bin");shadow=loadSource(device,directory/"shadow.bin");
    }
};
struct FieldFilterLight {
    std::uint32_t fieldObjectId{},sourceOrdinal{};
    ID3D11ShaderResourceView* depth{};
    Matrix worldToShadow{};
    Float4 direction{},color{};
};
struct FieldFilterProgress {UINT mode{1},phase{};std::int8_t layer{},iteration{};bool parity{};};
class FieldPrograms {
public:
    // Fresh RefProbe shader initialization supplies these values. A real later
    // screen-module publication can replace them without resetting wrappers.
    void applyCurrentTuning(float indirect,float specular,float biasX,float biasY) {
        indirectWeight_={indirect,0,0,specular};traceBias_={biasX,biasY,0,0};
    }
    void prepare(ID3D11Device* device,const fs::path& directory) {
        for(UINT pass:{0u,1u,2u,6u,7u,8u,9u,10u,11u})
            programs_[pass].prepare(device,directory/("pass-"+std::to_string(pass)),pass!=6&&pass!=7&&pass!=11);
        auto vertex=load(directory/"pass-6"/"vs.dxbc");check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&vertex_),"Reflection fullscreen original VS1");
        D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&raster,&raster_),"Reflection original fullscreen raster");
        D3D11_DEPTH_STENCIL_DESC depth{};depth.DepthFunc=D3D11_COMPARISON_ALWAYS;
        check(device->CreateDepthStencilState(&depth,&depth_),"Reflection original fullscreen depth disabled");
        D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_RED|D3D11_COLOR_WRITE_ENABLE_GREEN|D3D11_COLOR_WRITE_ENABLE_BLUE;
        check(device->CreateBlendState(&blend,&blend_),"Reflection original filter RGB writes");
        samplers_.prepare(device,directory/"samplers");
    }
    // FD7160/FD9A10: exact color2/color3/color1 conversion. CPU owner commits
    // its retained wrapper cursor only after this method returns successfully.
    void convert(ID3D11DeviceContext* context,FieldStorage& field,UINT ordinal,const CaptureTargets& cubes) {
        if(ordinal>=field.probes)throw std::out_of_range("Reflection source cell ordinal");
        const UINT plane=field.columns*field.rows;
        for(UINT channel=0;channel<3;++channel) {
            const UINT pass=channel==1?1:0,color=channel==0?2:channel==1?3:1;
            auto& program=programs_.at(pass);reset(context);
            program.vector("gv_info_out",Float4{float((ordinal%field.columns)*field.shape.tile),float(((ordinal%plane)/field.columns)*field.shape.tile),float(ordinal/plane%field.layers+channel*field.layers),0});
            program.vector("gv_tiledoct_pitch_octsize",Float4{float(field.columns),float(field.rows),float(field.layers),float(field.shape.tile)});
            if(program.has("gv_info_in"))program.vector("gv_info_in",Float4{256,0,0,0});
            auto input=cubes.cube(color);auto sampler=samplers_.cube.Get();auto output=field.capture.uav.Get();
            context->CSSetShaderResources(8,1,&input);context->CSSetSamplers(8,1,&sampler);context->CSSetUnorderedAccessViews(0,1,&output,nullptr);
            program.dispatchGroups(context,(field.shape.tile+7)/8,(field.shape.tile+7)/8);reset(context);
        }
        if(ordinal==field.probes-1) {
            auto& program=programs_.at(10);
            for(UINT layer=0;layer<field.layers;++layer) {
                program.vector("gv_info_out",Float4{float(layer),0,0,0});program.vector("gv_info_in",Float4{float(layer),0,0,0});
                program.vector("gv_tiledoct_pitch_octsize",Float4{float(field.columns),float(field.rows),float(field.layers),float(field.shape.atlas)/field.columns});
                auto input=field.capture.srv.Get();auto output=field.sky.uav.Get();context->CSSetShaderResources(9,1,&input);context->CSSetUnorderedAccessViews(4,1,&output,nullptr);
                program.dispatchGroups(context,field.columns,field.rows);reset(context);
            }
        }
    }
    void advanceDirect(ID3D11DeviceContext* context,FieldStorage& field,FieldFilterProgress& progress,const FieldFilterLight& actualLight) {
        if(progress.mode>1)return;if(progress.phase==0)progress.phase=1;if(progress.phase!=1)return;
        if(!actualLight.depth)throw std::runtime_error("Reflection actual field-owned shadow map was not rendered");
        do {
            UINT layer=currentLayer(progress,field.layers);
            submit(context,field,progress,6,layer,0,false,&actualLight);
            submit(context,field,progress,2,layer,0,false,nullptr);
            progress.layer=layer<field.layers-1?std::int8_t(layer+1):0;
            if(progress.layer==0)progress.phase=2;
        }while(progress.mode==0&&progress.phase==1);
    }
    void advanceFiltering(ID3D11DeviceContext* context,FieldStorage& field,FieldFilterProgress& progress) {
        const UINT initial=progress.phase;
        do {
            if(progress.mode>1)return;
            if(progress.phase==2) {
                UINT layer=currentLayer(progress,field.layers);
                submit(context,field,progress,7,layer,0,false,nullptr);
                submit(context,field,progress,2,layer,0,true,nullptr);
                if(layer==field.layers-1)submit(context,field,progress,8,layer,0,false,nullptr);
                progress.layer=layer<field.layers-1?std::int8_t(layer+1):0;
                if(progress.layer==0)progress.iteration=std::int8_t(progress.iteration+1);
                if(progress.iteration>=4){progress.phase=3;progress.iteration=0;}
            } else if(progress.phase==3) {
                if(progress.mode==1&&initial!=3)return;
                submit(context,field,progress,9,0,0,false,nullptr);
                submit(context,field,progress,8,0,0,true,nullptr);
                for(UINT layer=0;layer<field.layers;++layer)for(UINT mip=1;mip<field.mips;++mip)submit(context,field,progress,11,layer,mip,false,nullptr);
                progress.layer=0;progress.phase=0;progress.parity=!progress.parity;
            } else return;
        }while(progress.mode==0&&(progress.phase==2||progress.phase==3));
    }
    static void reset(ID3D11DeviceContext* context){unbindCompute(context);unbindPixel(context);unbindOutputs(context);}
private:
    static UINT currentLayer(const FieldFilterProgress& p,UINT layers){return std::min(UINT(int(p.layer)),layers-1);}
    static Float4 reciprocal(Float4 value){for(auto& x:value)x=1/x;return value;}
    void parameters(OriginalProgram& p,const FieldStorage& f,const FieldFilterProgress& progress,UINT pass,UINT layer,UINT mip,bool alternate,const FieldFilterLight* light) {
        const UINT targetWidth=pass==7?f.propagationExtent:pass==11?f.shape.atlas>>mip:f.shape.atlas;
        Float4 tiles{float(f.columns),float(f.rows),float(f.layers),float(f.shape.tile)},pitch{float(f.shape.x),float(f.shape.y),float(f.shape.z),f.shape.pitch};
        Float4 input{float(layer),0,0,0},output{};
        if(pass==2){tiles[3]=float(alternate?f.propagationExtent:f.shape.atlas)/f.columns;input[0]=alternate?0:float(layer);output[0]=float(layer);}
        else if(pass==6||pass==7){output={1.f/targetWidth,1.f/targetWidth,1.f/(float(targetWidth)/f.columns),0};input[1]=pass==7?float(progress.iteration+1):0;}
        else if(pass==8)output[0]=alternate?1.f:0.f;
        else if(pass==9)output={1.f/f.shape.atlas,1.f/f.shape.atlas,1.f/(float(f.shape.atlas)/f.columns),float(retainedInput_)};
        else if(pass==11){output={1.f/targetWidth,float(targetWidth)/f.columns,0,0};tiles[3]=float(f.shape.atlas>>(mip-1))/f.columns;}
        auto set=[&](const char* name,const Float4& value){if(p.has(name))p.vector(name,value);};
        set("gv_info_out",output);set("gv_info_in",input);set("gv_probefield_org",Float4{f.shape.origin[0],f.shape.origin[1],f.shape.origin[2],0});
        set("gv_probefield_pitch_size",pitch);set("gv_probefield_pitch_size_rv",reciprocal(pitch));
        set("gv_tiledoct_pitch_octsize",tiles);set("gv_tiledoct_pitch_octsize_rv",reciprocal(tiles));set("gv_probe_scale_offset",Float4{f.shape.range,0,0,0});
        set("gv_indirect_weight",indirectWeight_);set("gv_trace_bias",traceBias_);
        if(pass==11)set("gv_mip_info",Float4{float(mip),float(mip)/float(f.mips-1),0,0});
        if(light){set("litDir",light->direction);set("litCol",light->color);if(p.has("mW2S"))p.matrix("mW2S",light->worldToShadow);}
    }
    void submit(ID3D11DeviceContext* context,FieldStorage& f,const FieldFilterProgress& progress,UINT pass,UINT layer,UINT mip,bool alternate,const FieldFilterLight* light) {
        reset(context);auto& program=programs_.at(pass);parameters(program,f,progress,pass,layer,mip,alternate,light);
        auto texture=[&](UINT slot,ID3D11ShaderResourceView* value,bool compute){if(compute)context->CSSetShaderResources(slot,1,&value);else context->PSSetShaderResources(slot,1,&value);};
        auto sampler=[&](UINT slot,ID3D11SamplerState* value,bool compute){if(compute)context->CSSetSamplers(slot,1,&value);else context->PSSetSamplers(slot,1,&value);};
        auto uav=[&](UINT slot,ID3D11UnorderedAccessView* value){context->CSSetUnorderedAccessViews(slot,1,&value,nullptr);};
        if(pass==6||pass==7) {
            texture(2,f.capture.srv.Get(),false);
            if(pass==6){texture(0,light->depth,false);sampler(0,samplers_.shadow.Get(),false);}
            else sampler(2,samplers_.octmap.Get(),false);
            auto target=pass==6?f.scratch.targets.at(layer).Get():f.propagation.targets.at(0).Get();
            ID3D11UnorderedAccessView* coefficients=progress.iteration==0?f.working.uav.Get():f.propagated.uav.Get();
            if(pass==7)context->OMSetRenderTargetsAndUnorderedAccessViews(1,&target,nullptr,2,1,&coefficients,nullptr);
            else context->OMSetRenderTargets(1,&target,nullptr);
            raster(context,program,target,pass==6?f.shape.atlas:f.propagationExtent);
            retainedInput_=int(layer);
        } else if(pass==2) {
            texture(10,alternate?f.propagation.srv.Get():f.scratch.srv.Get(),true);uav(2,alternate?f.propagated.uav.Get():f.working.uav.Get());
            program.dispatchGroups(context,f.columns,f.rows);retainedInput_=alternate?0:int(layer);
        } else if(pass==8) {
            uav(2,alternate?f.sh.uav.Get():f.working.uav.Get());uav(3,alternate?f.working.uav.Get():f.propagated.uav.Get());
            program.dispatchGroups(context,f.columns*f.rows*f.layers,1);
        } else if(pass==9) {
            texture(9,f.capture.srv.Get(),true);sampler(9,samplers_.octmap.Get(),true);texture(10,f.scratch.srv.Get(),true);
            uav(1,f.radiance.uav.Get());uav(2,f.working.uav.Get());program.dispatchGroups(context,(f.shape.atlas+7)/8,(f.shape.atlas+7)/8,f.layers);
        } else if(pass==11) {
            texture(3,f.radiance.previousMips.at(mip-1).Get(),false);sampler(3,f.mips>1?samplers_.radianceMipped.Get():samplers_.radianceSingle.Get(),false);
            auto target=f.radiance.targets.at(mip*f.layers+layer).Get();context->OMSetRenderTargets(1,&target,nullptr);
            raster(context,program,target,f.shape.atlas>>mip);retainedInput_=int(layer);
        }
        reset(context);
    }
    void raster(ID3D11DeviceContext* context,OriginalProgram& program,ID3D11RenderTargetView* target,UINT extent)const {
        const float clear[4]{};context->ClearRenderTargetView(target,clear);
        D3D11_VIEWPORT viewport{0,0,float(extent),float(extent),0,1};context->RSSetViewports(1,&viewport);context->RSSetState(raster_.Get());
        context->OMSetDepthStencilState(depth_.Get(),0);context->OMSetBlendState(blend_.Get(),nullptr,~0u);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(vertex_.Get(),nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        program.bind(context);context->Draw(3,0);
    }
    Float4 indirectWeight_{.8f,0,0,1},traceBias_{.01f,.01f,0,0};
    std::map<UINT,OriginalProgram> programs_;FieldSamplers samplers_;ComPtr<ID3D11VertexShader> vertex_;
    ComPtr<ID3D11RasterizerState> raster_;ComPtr<ID3D11DepthStencilState> depth_;ComPtr<ID3D11BlendState> blend_;int retainedInput_{};
};
} // namespace San14::Render::Reflection
