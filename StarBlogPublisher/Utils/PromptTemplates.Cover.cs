using System.Collections.Generic;

namespace StarBlogPublisher.Utils;

public static partial class PromptTemplates {
    public static List<PromptTemplate> Cover = [
        new PromptTemplate {
            Key = "Minimalism",
            Name = "极简风格（Minimalism）",
            Prompt =
                """
                Please generate a minimalist-style cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: Use clean composition with a focus on simplicity and whitespace. The image should feature abstract shapes or iconography on a solid-color background. Avoid complex scenes or human figures. The colors should be fresh and modern with a strong design sense.

                Style Keywords: minimalism, whitespace, modern, abstract, icon-style
                Aspect Ratio: 16:9, image only, no text
                """
        },
        new PromptTemplate {
            Key = "TechStyle",
            Name = "科技感风格（Tech Style）",
            Prompt =
                """
                Please generate a tech-themed cover illustration for the following technical blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: The image should convey a strong sense of modern technology. Include elements such as code streams, circuit boards, chips, AI patterns, and data flow. Use dark tones with blue lights and glowing lines to emphasize a futuristic, high-tech look.

                Style Keywords: technology, digital, programming, network, cyber, futuristic
                Aspect Ratio: 16:9, image only, no text
                """
        },
        new PromptTemplate {
            Key = "AttractiveFemale",
            Name = "美女吸睛风（Attractive Female）",
            Prompt =
                """
                Please generate an eye-catching cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}
                Content: {{content}}

                Requirements: The central figure should be a modern, stylish, and intelligent-looking female character. The background can incorporate technology, internet, urban, or abstract themes. The image should be attractive but remain tasteful and appropriate.

                Style Keywords: high-attractiveness female, fashion, tech background, elegant, visually engaging
                Aspect Ratio: 16:9, image only, no text
                """
        },
        new PromptTemplate {
            Key = "OpenSourcePoster",
            Name = "开源纪念风（Open Source Poster）",
            Prompt =
                """
                Please generate a commemorative open-source style cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: Design a poster-style image that reflects the spirit of open source and collaboration. Consider including symbols like code snippets, GitHub, community visuals, or the Earth. Use bold, inspiring colors with a sense of unity and contribution.

                Style Keywords: open source, contribution, poster design, collaboration, code community
                Aspect Ratio: 16:9, image only, no text
                """
        },
        new PromptTemplate {
            Key = "Futuristic",
            Name = "未来感风格（Futuristic）",
            Prompt =
                """
                Please generate a highly futuristic cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: The scene should feature futuristic technology, cyberpunk aesthetics, and AI-inspired elements. May include futuristic cities, digital humans, virtual interfaces, or a cyber world. Use modern and vibrant color schemes to create a sci-fi atmosphere.

                Style Keywords: future, AI, AIGC, virtual space, cyberpunk, digital human, sci-fi
                Aspect Ratio: 16:9, image only, no text
                """
        },
        new PromptTemplate {
            Key = "UrbanElegance",
            Name = "城市优雅风（Urban Elegance）",
            Prompt =
                """
                Please generate an attractive cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: The central figure should be a stylish, confident modern woman walking in an urban setting. Include elements such as skyscrapers, reflective glass buildings, and city streets. The composition should convey elegance and independence.

                Style Keywords: urban, elegance, high heels, confident walk, modern architecture, city fashion
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "WarmSmileCozy",
            Name = "温暖治愈风（Warm Smile & Cozy）",
            Prompt =
                """
                Please generate a cozy and heartwarming cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A gentle female character with a warm smile sitting in a cozy cafe with soft sunlight. The atmosphere should evoke comfort, warmth, and a peaceful afternoon.

                Style Keywords: coffee shop, warm light, cozy, gentle smile, relaxing afternoon, soft colors
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "BeachCharm",
            Name = "夏日清新风（Beach Charm）",
            Prompt =
                """
                Please generate a refreshing summer-themed cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A radiant girl enjoying the breeze on a sunny beach. Long hair fluttering, dressed in summer clothes, with the ocean and sky in the background. The image should be vibrant and full of life.

                Style Keywords: beach, sunshine, ocean breeze, summer dress, light and airy, youthful energy
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "ElegantPortrait",
            Name = "高质人像风（Elegant Portrait）",
            Prompt =
                """
                Please generate a high-quality portrait-style cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A detailed, elegant headshot or half-body portrait of a female character in a studio-like setting. The focus should be on facial features, refined skin texture, and artistic lighting.

                Style Keywords: studio portrait, close-up, soft lighting, elegant beauty, photo realism
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "GirlNextDoor",
            Name = "邻家女孩风（Girl Next Door）",
            Prompt =
                """
                Please generate a charming and approachable cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A natural, cheerful girl sitting by a window with a book, sunlight spilling over her shoulder. The composition should feel intimate, warm, and down-to-earth.

                Style Keywords: natural smile, book, soft sunlight, casual clothing, window seat, gentle atmosphere
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "StreetStyle",
            Name = "动感街拍风（Street Style）",
            Prompt =
                """
                Please generate a dynamic and fashionable street-style cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A trendy girl walking confidently down a city street, showcasing modern fashion. Capture motion, attitude, and the energy of street culture.

                Style Keywords: street fashion, dynamic walking, graffiti, cool attitude, modern youth, trendy outfit
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "RomanticGarden",
            Name = "浪漫花园风（Romantic Garden）",
            Prompt =
                """
                Please generate a dreamy and floral-themed cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A romantic young woman in a flowing dress surrounded by blooming flowers in a sunlit garden. The image should feel like a fairytale, soft and dreamy.

                Style Keywords: flowers, long dress, sunlight, dreamy, romantic, garden setting
                Aspect Ratio: 16:9, image only, no text
                """
        },

        new PromptTemplate {
            Key = "MagazineCover",
            Name = "杂志封面风（Magazine Cover）",
            Prompt =
                """
                Please generate a fashion-forward magazine-style cover illustration for the following blog post:

                Title: {{title}}
                Summary: {{summary}}

                Requirements: A stunning female model in a striking pose, with a minimalist or high-fashion backdrop. Emphasize bold aesthetics and visual tension as seen in luxury fashion magazines.

                Style Keywords: fashion model, editorial style, dramatic pose, neutral background, high-end vibe
                Aspect Ratio: 16:9, image only, no text
                """
        },
        
        new PromptTemplate {
            Key = "BeCreativeGirl",
            Name = "妹子 - 自由发挥",
            Prompt = 
                """
                请为这篇文章生成纯英文的AI画图提示词，画面主体是一个可爱的年轻女高中生，风格、内容请自由发挥。
                
                标题：{{title}}
                简介：{{summary}}
                
                要求：不要输出多余的说明、解释或其他格式。
                """
        },
        
        new PromptTemplate {
            Key = "BeCreativeGirl",
            Name = "喵喵 - 自由发挥",
            Prompt = 
                """
                请为这篇文章生成纯英文的AI画图提示词，画面主体是可爱的猫猫，风格、内容请自由发挥。

                标题：{{title}}
                简介：{{summary}}

                要求：不要输出多余的说明、解释或其他格式。
                """
        },
    ];
}