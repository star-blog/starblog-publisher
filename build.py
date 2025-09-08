import os
import shutil
import subprocess
import zipfile
from datetime import datetime
import platform

# 配置信息
VERSION = "1.10.3"

def get_aot_platforms():
    """获取支持AOT的平台列表"""
    p = platform.system().lower()
    if p == "windows":
        return ["win-x64"]
    elif p == "linux":
        return ["linux-x64"]
    elif p == "darwin":
        return ["osx-x64"]
    else:
        raise ValueError(f"不支持的平台: {p}")

# 构建配置
BUILD_CONFIGS = {
    "self-contained": {
        "args": "--self-contained true -p:PublishSingleFile=true",
        "platforms": ["win-x64", "linux-x64", "osx-x64"]
    },
    "aot": {
        "args": "/p:PublishAot=true /p:TrimMode=full /p:InvariantGlobalization=true /p:IlcGenerateStackTraceData=false /p:IlcOptimizationPreference=Size /p:IlcFoldIdenticalMethodBodies=true /p:JsonSerializerIsReflectionEnabledByDefault=true",
        "platforms": get_aot_platforms()
    }
}

# 默认构建配置
active_profiles = [
    # "self-contained",
    "aot"
]

# 如果在 GitHub Actions 中运行，只构建指定平台
if "GITHUB_PLATFORM" in os.environ:
    BUILD_CONFIGS["self-contained"]["platforms"] = [os.environ["GITHUB_PLATFORM"]]


def get_build_command(profile, target_system):
    """根据配置和目标系统生成构建命令"""
    if profile not in BUILD_CONFIGS:
        raise ValueError(f"不支持的构建配置: {profile}")
    
    config = BUILD_CONFIGS[profile]
    if target_system not in config["platforms"]:
        raise ValueError(f"在 {profile} 模式下不支持目标系统: {target_system}")
    
    return f"dotnet publish -c Release -r {target_system} {config['args']}"


# 符号文件扩展名列表
SYMBOL_FILE_EXTENSIONS = [
    '.pdb',    # Windows符号文件
    '.dbg',    # Linux符号文件
    '.dSYM',   # macOS符号文件
    '.dwarf',  # DWARF调试信息文件
    '.sym'     # 通用符号文件
]

def clean_publish_dir(publish_dir):
    """清理发布目录，移除所有平台的符号文件"""
    for root, _, files in os.walk(publish_dir):
        for file in files:
            # 检查文件是否为任何已知的符号文件类型
            if any(file.endswith(ext) for ext in SYMBOL_FILE_EXTENSIONS):
                file_path = os.path.join(root, file)
                print(f"移除符号文件: {file_path}")
                os.remove(file_path)


def create_zip(source_dir, output_file):
    """创建最大压缩率的zip文件"""
    with zipfile.ZipFile(output_file, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for root, _, files in os.walk(source_dir):
            for file in files:
                file_path = os.path.join(root, file)
                arcname = os.path.relpath(file_path, source_dir)
                zf.write(file_path, arcname)


def build_and_package(profile, system):
    """构建并打包指定配置和系统的发布包"""
    print(f"\n开始构建 {profile} - {system}...")

    try:
        # 创建发布命令
        build_cmd = get_build_command(profile, system)
        project_dir = os.path.join(os.path.dirname(__file__), "StarBlogPublisher")
        publish_dir = os.path.join(project_dir, "bin", "Release", "net8.0", system, "publish")

        # 执行构建
        subprocess.run(build_cmd.split(), cwd=project_dir, check=True)

        # 清理PDB文件
        clean_publish_dir(publish_dir)

        # 创建dist目录
        dist_dir = os.path.join(os.path.dirname(__file__), "dist")
        os.makedirs(dist_dir, exist_ok=True)

        # 创建zip文件
        zip_filename = f"StarBlogPublisher_{VERSION}-{system}-{profile}.zip"
        zip_path = os.path.join(dist_dir, zip_filename)
        create_zip(publish_dir, zip_path)

        print(f"打包完成: {zip_filename}")
        return True
    except Exception as e:
        print(f"构建失败: {str(e)}")
        return False


def main():
    # 确保dist目录存在并清空
    dist_dir = os.path.join(os.path.dirname(__file__), "dist")
    if os.path.exists(dist_dir):
        shutil.rmtree(dist_dir)
    os.makedirs(dist_dir)
    
    success_count = 0
    total_systems = 0

    # 获取当前配置支持的目标系统
    for active_profile in active_profiles:
        target_systems = BUILD_CONFIGS[active_profile]["platforms"]
        total_systems += len(target_systems)
    
        # 构建所有支持的目标系统
        for system in target_systems:
            if build_and_package(active_profile, system):
                success_count += 1

    print(f"\n构建完成！成功: {success_count}/{total_systems}")



if __name__ == "__main__":
    main()
