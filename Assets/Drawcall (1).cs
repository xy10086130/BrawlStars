using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/*
 * Drawcall:CPU对图形绘制接口（DriectX,OPENGL）的调用,CPU通过调用图形库命令GPU进行渲染
 * dc次数越大，游戏性能损耗越大
 * 造成DC性能瓶颈是CPU还是GPU？
 * CPU
 *
 * 资源优化标准
 * mesh
 * 动态模型：面片数，材质数，骨骼数
 * 静态模型：顶点数
 * 音频
 * 压缩格式，mp3,bgm
 * 非压缩。wav,音效
 * 1.尽量使用单声道
 * 2.压缩率
 * 3.声音加载方式
 * 贴图
 * 1.图集
 * 2.压缩方式
 * shader
 * 1.尽量减少复杂的运算
 * GC
 * 1.耗时
 * 2.内存碎片化
 *
 *
 * 批处理
 * 目的：减少DC
 * 动态批处理
 * 原理：每一帧把可以进行批处理的模型网格进行合并，再把合并后的模型数据传递给GPU，然后使用同一个材质进行渲染
 * 1.Unity自动完成
 * 2.同一材质，同一shader,同一网格
 * 3.顶点属性
 * 4.多pass的shader会 中断批处理
 * 5.参数不一致（缩放值，shader变体）
 * 静态批处理
 * 原理：只在游戏运行开始阶段，把需要进行静态批处理的模型合并到一个新的网格中，合并后模型数据在游戏运行无法移动
 * 优点：只进行一次合并操作，比动态批处理更高效
 * 缺点：合并后物体无法移动，占用更多的内存
 *
 * GPU Instancing
 * 原理：对于使用同一个网格，同一个材质，同一个shader，在渲染时，CPU可以把他们不同的属性（位置，大小，旋转，颜色等）和其他需要实例化的材质信息组装成一个buffer，传递给GPU，GPU在渲染这些物体的时候会有全局的实例化参数，也有每个实例id,每渲染完一个物体就会递增
 * 优点：
 * 1.每次调用时仅渲染相同网格，但是允许每个实例使用不同的参数，以此增加变化从而外挂上的重复，从而降低DC
 * 2.Unity自动完成
 * 3.GPU Instaning不支持SkinnerMeshRenderer
 *
 * SRP Batching
 * 原理：不依赖网格合并，通过统一管理材质属性，从而实现多对象合批，即使多个物体使用不同的材质实例时，只要他们的材质属性布局相同且是同一shader变体，srp就会将这些属性合并到一个CBUFF中，合并为一个dc
 *
 * 动态批处理：适用于低面数（低模，UI，简单的例子特效）且无法使用gpu instancing或者srp batcing的场景
 * gpu instancing:适用于大量且完全相同的物体
 * srp batcing:适用于大量使用相同shader但网格不同的物体
 * gpu instancing和srp batching是无法共存
 *
 *
 * UI批处理
 *如果UI元素会改变数值或者位置，会影响批处理
 * 建议：
 *1.将更新频率不同的UI放在不同的Canvs下
 * 2.相同的canvas中ui元素z值必须相同，否则会影响批处理
 * 3.相同的canvas中的ui元素使用相同的纹理和材质，shader，这样就不会影响批处理
 * 4.相同的canvas中的ui元素使用相同的剪裁矩阵
 * 5.尽可能的降低帧率，因为UI不需要那么高的刷新频率
 *
 * 贴图优化：
 * 1.是否需要alpha通道
 * 2.是否需要2次冥
 * 3.是否需要图片的读写权限
 * 4.图片的尺寸大小
 * 5.是否需要多级贴图
 *
 * UI优化：
 * UI合批
 * 原理：指Canvas合并UI元素的网格，并且生成发生给渲染管线的命令
 * 合并规则：在同一个canvas中，将同层级的，同类型的，同材质的，同shader的元素进行合并，从而减少dc
 * canvas重构
 * ui元素发生变化（位置，颜色，文本内容）时 需要重新计算网格和渲染批次
 * 几何重构：更新顶点，uv,颜色的几何数据，发生在canvasrenderer中
 * 批次重构：合并相同材质的UI元素，从而降低dc，发生在canvas合批阶段
 * 触发条件：ui元素位置、大小变化，文本内容变化，材质，纹理修改等
 * 优化canvas重构：
 * 1.动静分离
 * 原理：将静态的UI元素和动态的UI元素放在不同的canvas中，动态的UI元素只进行canvas重构
 * 2.拆分过重的UI
 * 3.减少UI元素的更新频率，避免频繁使用setactive
 * 4.图集优化
 * 5.直接修改image.color会触发顶点更新，但是对材质进行修改（自定义shader）不会
 *
 * ui优化
 * 1.UI预加载
 * 2.网格重构优化
 * 3.字体优化
 *
 * 总结：
 * ui的优化的核心目的：减少重构的频率和降低合批开销
 *
 * 冗余资源，重复资源
 *
 * LOD技术 多层次细节技术
 * 原理：根据距离相机的远近，加载不同细节的模型
 * 优点：根据距离相机的远近，加载不同细节的模型，从而减少GPU的负担，降低DC
 * 缺点：增加内存开销
 *
 * mipmap技术 多级贴图
 * 原理：根据距离相机的远近，加载不同细节的图片
 * 缺点：增加内存开销
 *
 * 遮挡剔除技术
 * 原理：只对在视野范围内的游戏物体进行渲染
 *
 * 光照贴图技术
 * 原理：通过预先计算光照信息，从而减少GPU的计算负担，将相同的信息放在一张贴图上
 *
 * mesh合并
 * 原理：将多个模型的网格数据进行合并，从而减少GPU的负担，降低DC

 */
public class Drawcall : MonoBehaviour {

	// Use this for initialization
	void Start ()
	{

		MeshFilter[] meshFilters = this.GetComponentsInChildren<MeshFilter>();
		CombineInstance[] combine = new CombineInstance[meshFilters.Length];
		for (int i = 0; i < meshFilters.Length; i++)
		{
			combine[i].mesh = meshFilters[i].sharedMesh;
			combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
		}
		Mesh mesh = new Mesh();
		mesh.CombineMeshes(combine);
		this.GetComponent<MeshFilter>().sharedMesh = mesh;
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
