using OpenTK.Graphics.OpenGL4;
using SmoothGL.Content;
using SmoothGL.Graphics.Shader.Internal;

namespace SmoothGL.Graphics.Shader;

/// <summary>
/// Fork of SmoothGL's ShaderProgram class which exposes a bunch of previously
/// private important functionality. Taken from:
/// https://github.com/jnagykuhlen/SmoothGL/blob/main/SmoothGL/Graphics/Shader/ShaderProgram.cs
/// </summary>
public class Shader : GraphicsResource, IHotSwappable<Shader> {
    private static int currentProgramId;

    public int programId;
    private Dictionary<string, ShaderProgramUniform> _uniforms = new(StringComparer.Ordinal);
    private Dictionary<string, ShaderUniformBlock> _uniformBlocks = new(StringComparer.Ordinal);

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
        string? tessellationControlShaderCode,
        string? tessellationEvaluationShaderCode,
        string? geometryShaderCode,
        string fragmentShaderCode)
    {
        var shaderIds = new List<int>(5);
        try {
            shaderIds.Add(CreateShader(ShaderType.VertexShader, vertexShaderCode));

            if (tessellationControlShaderCode != null)
                shaderIds.Add(CreateShader(ShaderType.TessControlShader, tessellationControlShaderCode));

            if (tessellationEvaluationShaderCode != null)
                shaderIds.Add(CreateShader(ShaderType.TessEvaluationShader, tessellationEvaluationShaderCode));

            if (geometryShaderCode != null)
                shaderIds.Add(CreateShader(ShaderType.GeometryShader, geometryShaderCode));

            shaderIds.Add(CreateShader(ShaderType.FragmentShader, fragmentShaderCode));

            programId = LinkProgram(shaderIds);
        } finally {
            foreach (var shaderId in shaderIds) {
                if (shaderId != 0) {
                    GL.DeleteShader(shaderId);
                }
            }
        }

        InitializeUniforms();
    }

    /// <summary>
    /// Gets all uniforms defined by this shader program.
    /// </summary>
    public IEnumerable<ShaderUniform> Uniforms => _uniforms.Values;

    /// <summary>
    /// Gets all uniform blocks defined by this shader program.
    /// </summary>
    public IEnumerable<ShaderUniformBlock> UniformBlocks => _uniformBlocks.Values;

    /// <summary>
    /// Gets the uniform with the specified name. Returns null if such uniform does not exist.
    /// Note that uniform value changes are not communicated to the GPU until this shader
    /// program's <see cref="Use" /> method is called again.
    /// </summary>
    /// <param name="name">Name of the uniform.</param>
    /// <returns>Uniform.</returns>
    public ShaderUniform? Uniform(string name) => _uniforms.GetValueOrDefault(name);

    /// <summary>
    /// Gets the uniform block with the specified name. Returns null if such uniform block does not exist.
    /// </summary>
    /// <param name="name">Name of the uniform block.</param>
    /// <returns>Uniform block.</returns>
    public ShaderUniformBlock? UniformBlock(string name) => _uniformBlocks.GetValueOrDefault(name);

    /// <summary>
    /// Gets a value indicating whether this shader program is currently in use for subsequent draw operations,
    /// i.e., its <see cref="Use" /> method was called after using any other shader program.
    /// </summary>
    public bool IsActive => currentProgramId == programId;

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
            throw new ShaderProgramLinkException(message);
        }

        return programId;
    }

    public static int CreateShader(ShaderType shaderType, string shaderCode) {
        var shaderId = GL.CreateShader(shaderType);
        GL.ShaderSource(shaderId, shaderCode);
        GL.CompileShader(shaderId);
        GL.GetShader(shaderId, ShaderParameter.CompileStatus, out var compiled);

        if (compiled == 0) {
            var message = GL.GetShaderInfoLog(shaderId);
            GL.DeleteShader(shaderId);
            throw new ShaderCompilationException(message, (ShaderStage)shaderType, shaderCode);
        }

        return shaderId;
    }

    private void InitializeUniforms() {
        GL.UseProgram(programId);

        GL.GetProgram(programId, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);
        GL.GetProgram(programId, GetProgramParameterName.ActiveUniformBlocks, out var numberOfUniformBlocks);

        _uniforms.EnsureCapacity(numberOfUniforms);
        _uniformBlocks.EnsureCapacity(numberOfUniformBlocks);

        var uniformBlockElements = new List<UniformBufferElement>[numberOfUniformBlocks];
        for (var i = 0; i < numberOfUniformBlocks; ++i) {
            uniformBlockElements[i] = new List<UniformBufferElement>();
        }

        var maxNumberOfTextures = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        var textureIndex = 0;

        for (var uniformIndex = 0; uniformIndex < numberOfUniforms; ++uniformIndex) {
            var uniformName = GL.GetActiveUniform(programId, uniformIndex, out var uniformSize, out var uniformRawType).Replace("[0]", "");
            var uniformLocation = GL.GetUniformLocation(programId, uniformName);
            GL.GetActiveUniforms(programId, 1, ref uniformIndex, ActiveUniformParameter.UniformBlockIndex, out var uniformBlockIndex);
            var uniformType = (ShaderUniformType)uniformRawType;
            
            if (!Enum.IsDefined(typeof(ShaderUniformType), uniformType)) {
                // Change from torf: stop library from freaking out about sampler array uniforms
                // throw new ShaderUniformException($"The uniform type {uniformRawType} specified in the shader for uniform {uniformName} is not supported.");
                continue;
            }


            if (uniformBlockIndex == -1) {
                if (uniformType.IsSampler()) {
                    if (textureIndex + uniformSize > maxNumberOfTextures) {
                        throw new ShaderUniformException($"Texture uniform {uniformName} exceeds the limit of {maxNumberOfTextures} texture units.");
                    }

                    var textureIndices = Enumerable.Range(textureIndex, uniformSize).ToArray();
                    GL.Uniform1(uniformLocation, uniformSize, textureIndices);

                    uniformLocation = textureIndex;
                    textureIndex += uniformSize;
                }

                var uniform = new ShaderProgramUniform(uniformName, uniformType, uniformSize, uniformLocation);
                _uniforms.Add(uniformName, uniform);
            } else {
                GL.GetActiveUniforms(programId, 1, ref uniformIndex, ActiveUniformParameter.UniformOffset, out var uniformOffset);
                uniformBlockElements[uniformBlockIndex].Add(new UniformBufferElement(uniformName, uniformType, uniformSize, uniformOffset));
            }
        }
    }

    public void ApplyUniforms() {
        foreach (var uniform in _uniforms.Values) {
            try {
                uniform.Apply();
            } catch (ShaderUniformException e) {
                // This is non-fatal, don't bail if a uniform hasn't been set
                // yet (because it may be safely defaulted in the shader)
            }
        }
    }

    public int GetUniformLocation(string name) {
        return GL.GetUniformLocation(programId, name);
    }

    /// <summary>
    /// Communicates the uniform values to the GPU and uses this shader program for all
    /// subsequent drawing operations.
    /// </summary>
    public void Use() {
        CheckDisposed();

        if (currentProgramId != programId) {
            GL.UseProgram(programId);
            currentProgramId = programId;
        }

        foreach (var uniformBlock in UniformBlocks) {
            uniformBlock.Buffer?.Bind(uniformBlock.Location);
        }
    }

    protected override void FreeResources() {
        if (currentProgramId == programId)
            currentProgramId = 0;

        if (programId != 0)
            GL.DeleteProgram(programId);
    }

    void IHotSwappable<Shader>.HotSwap(Shader other) {
        foreach (var uniform in Uniforms) {
            var value = uniform.Value;
            var otherUniform = other.Uniform(uniform.Name);

            if (value != null && otherUniform is { Value: null } && otherUniform.Type == uniform.Type && otherUniform.Size == uniform.Size)
                otherUniform.SetValue(value);
        }

        foreach (var uniformBlock in UniformBlocks) {
            var buffer = uniformBlock.Buffer;
            var otherUniformBlock = other.UniformBlock(uniformBlock.Name);

            if (buffer != null && otherUniformBlock != null) {
                otherUniformBlock.SetBuffer(buffer);
            }
        }

        FreeResources();
        GC.SuppressFinalize(other);
        this.programId = other.programId;
        _uniforms = other._uniforms;
        _uniformBlocks = other._uniformBlocks;
    }
}
