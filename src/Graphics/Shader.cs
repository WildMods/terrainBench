using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OperationResult;
using static OperationResult.Helpers;

namespace terrainBench.Graphics;

/// <summary>
/// Fork of SmoothGL's ShaderProgram class which exposes a bunch of previously
/// private important functionality. Taken from:
/// https://github.com/jnagykuhlen/SmoothGL/blob/main/SmoothGL/Graphics/Shader/ShaderProgram.cs
/// </summary>
public class Shader {
    private static int currentProgramId;
    public int programId;

    /// <summary>
    /// Creates a new shader program with vertex and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public Shader(string vertexShaderCode, string fragmentShaderCode)
        : this(vertexShaderCode, null, null, null, fragmentShaderCode)
    {
    }

    /// <summary>
    /// Creates a new shader program with vertex, geometry and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="geometryShaderCode">Geometry shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public Shader(string vertexShaderCode, string geometryShaderCode, string fragmentShaderCode)
        : this(vertexShaderCode, null, null, geometryShaderCode, fragmentShaderCode)
    {
    }

    /// <summary>
    /// Creates a new shader program with vertex, tessellation and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="tessellationControlShaderCode">Tessellation control shader source code.</param>
    /// <param name="tessellationEvaluationShaderCode">Tessellation evaluation shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public Shader(string vertexShaderCode,
        string tessellationControlShaderCode,
        string tessellationEvaluationShaderCode,
        string fragmentShaderCode)
        : this(vertexShaderCode, tessellationControlShaderCode, tessellationEvaluationShaderCode, null, fragmentShaderCode)
    {
    }

    /// <summary>
    /// Creates a new shader program with vertex, tessellation, geometry and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="tessellationControlShaderCode">Tessellation control shader source code.</param>
    /// <param name="tessellationEvaluationShaderCode">Tessellation evaluation shader source code.</param>
    /// <param name="geometryShaderCode">Geometry shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public Shader(
        string vertexShaderCode,
        string? tessellationControlShaderCode, string? tessellationEvaluationShaderCode,
        string? geometryShaderCode, string fragmentShaderCode)
    {
        programId = CompileProgram(vertexShaderCode, tessellationControlShaderCode,
        tessellationEvaluationShaderCode, geometryShaderCode, fragmentShaderCode);
    }

    /// <summary>
    /// Creates a new shader program with vertex and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public static int CompileProgram(string vertexShaderCode, string fragmentShaderCode)
    {
        return CompileProgram(vertexShaderCode, null, null, null, fragmentShaderCode);
    }

    /// <summary>
    /// Creates a new shader program with vertex, geometry and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="geometryShaderCode">Geometry shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public static int CompileProgram(string vertexShaderCode, string geometryShaderCode, string fragmentShaderCode)
    {
        return CompileProgram(vertexShaderCode, null, null, geometryShaderCode, fragmentShaderCode);
    }

    /// <summary>
    /// Creates a new shader program with vertex, tessellation and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="tessellationControlShaderCode">Tessellation control shader source code.</param>
    /// <param name="tessellationEvaluationShaderCode">Tessellation evaluation shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public static int CompileProgram(string vertexShaderCode,
        string tessellationControlShaderCode,
        string tessellationEvaluationShaderCode, string fragmentShaderCode)
    {
        return CompileProgram(vertexShaderCode, tessellationControlShaderCode,
        tessellationEvaluationShaderCode, null, fragmentShaderCode);
    }
    /// <summary>
    /// Creates a new shader program with vertex, tessellation, geometry and fragment shader stage.
    /// </summary>
    /// <param name="vertexShaderCode">Vertex shader source code.</param>
    /// <param name="tessellationControlShaderCode">Tessellation control shader source code.</param>
    /// <param name="tessellationEvaluationShaderCode">Tessellation evaluation shader source code.</param>
    /// <param name="geometryShaderCode">Geometry shader source code.</param>
    /// <param name="fragmentShaderCode">Fragment shader source code.</param>
    public static int CompileProgram(string vertexShaderCode,
        string? tessellationControlShaderCode,
        string? tessellationEvaluationShaderCode,
        string? geometryShaderCode,
        string fragmentShaderCode)
    {
        int programId = 0;
        var shaderIds = new List<int>(5);
        try {
            shaderIds.Add(CompileShader(ShaderType.VertexShader, vertexShaderCode));

            if (tessellationControlShaderCode != null)
                shaderIds.Add(CompileShader(ShaderType.TessControlShader, tessellationControlShaderCode));

            if (tessellationEvaluationShaderCode != null)
                shaderIds.Add(CompileShader(ShaderType.TessEvaluationShader, tessellationEvaluationShaderCode));

            if (geometryShaderCode != null)
                shaderIds.Add(CompileShader(ShaderType.GeometryShader, geometryShaderCode));

            shaderIds.Add(CompileShader(ShaderType.FragmentShader, fragmentShaderCode));

            programId = LinkProgram(shaderIds);
        } catch (Exception e) {
            Console.WriteLine("Failed to compile shader: {0}", e.Message);
            return 0;
        } finally {
            foreach (var shaderId in shaderIds) {
                if (shaderId != 0) {
                    GL.DeleteShader(shaderId);
                }
            }
        }

        return programId;
    }

    /// <summary>
    /// Link compiled shader stages into a usable program
    /// </summary>
    /// <param name="shaderIds">The IDs of the compiled stages</param>
    /// <returns>Shader program ID</returns>
    /// <exception cref="Exception">Details the linker exception</exception>
    public static int LinkProgram(IReadOnlyCollection<int> shaderIds) {
        var programId = GL.CreateProgram();
        foreach (var shaderId in shaderIds) {
            GL.AttachShader(programId, shaderId);
        }

        GL.LinkProgram(programId);
        GL.GetProgram(programId, GetProgramParameterName.LinkStatus, out var linked);

        if (linked == 0) {
            var message = GL.GetProgramInfoLog(programId);
            GL.DeleteProgram(programId);
            throw new Exception(message);
        }

        return programId;
    }

    /// <summary>
    /// Compile a single shader stage from source code
    /// </summary>
    /// <exception cref="Exception">Details the compiler error.</exception>
    public static int CompileShader(ShaderType shaderType, string shaderCode) {
        var shaderId = GL.CreateShader(shaderType);
        GL.ShaderSource(shaderId, shaderCode);
        GL.CompileShader(shaderId);
        GL.GetShader(shaderId, ShaderParameter.CompileStatus, out var compiled);

        if (compiled == 0) {
            var message = GL.GetShaderInfoLog(shaderId);
            GL.DeleteShader(shaderId);
            throw new Exception($"Failed to compile {shaderType}: '{message}'");
        }

        return shaderId;
    }

    public static Result<int, ErrorStack> ProgramFromSingleStage(ShaderType stageType, string source) {
        var idList = new int[1];
        try {
            int shader = CompileShader(stageType, source);
            idList[0] = shader;
        } catch (Exception e) {
            return Err(new ErrorStack($"Failed to compile {stageType} shader", e));
        }
        
        try {
            int program = LinkProgram(idList);
            GL.DeleteShader(idList[0]);
            return program;
        } catch (Exception e) {
            return Err(new ErrorStack($"Failed to link program from the {stageType}", e));
        }
    }

    public int GetUniformLocation(string name) {
        return GL.GetUniformLocation(programId, name);
    }

    public void SetUniform(string name, int val) => Uniform.Set(programId, name, val);
    public void SetUniform(string name, float val) => Uniform.Set(programId, name, val);
    public void SetUniform(string name, Vector4 val) => Uniform.Set(programId, name, val);
    public void SetUniform(string name, Matrix4 val) => Uniform.Set(programId, name, val);

    /// <summary>
    /// Uses this shader program for all subsequent drawing operations.
    /// </summary>
    public void Use() {
        if (currentProgramId != programId && programId != 0) {
            GL.UseProgram(programId);
            currentProgramId = programId;
        }
    }

    public void FreeResources() {
        if (currentProgramId == programId)
            currentProgramId = 0;

        if (programId != 0)
            GL.DeleteProgram(programId);
    }

    /// <summary>
    /// Replace the current shader with a different one
    /// </summary>
    /// <param name="other">The shader to replace this one with</param>
    void HotSwap(Shader other) {
        FreeResources();
        GC.SuppressFinalize(other);
        this.programId = other.programId;
    }
}
